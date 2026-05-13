using System.Buffers;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace NovelAI_API;

public class NovelAiApi(IHttpClientFactory httpClientFactory)
{
    private const string BaseAddress = "https://api.novelai.net";
    private const string GenerateImageUrl = BaseAddress + "/ai/generate-image";

    public const int RandomSeedValue = -1;

    private string ApiKey { get; set; } = string.Empty;

    private static readonly JsonWriterOptions jsonWriterOptions = new() { Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping, Indented = false };

    public enum ImageModelType
    {
        // V1
        AnimeCurated,
        AnimeFull,
        Furry,

        // Inpainting V1
        InpaintingAnimeCurated,
        InpaintingAnimeFull,
        InpaintingFurry,

        // V2
        AnimeV2,

        // V3
        AnimeV3,
        FurryV3,

        // Inpainting V3
        InpaintingAnimeV3,
        InpaintingFurryV3,

        // V4 Preview/Curated
        AnimeV4Preview,
        AnimeV4Full,

        // Inpainting V4
        InpaintingAnimeV4Curated,
        InpaintingAnimeV4Full,

        // V4.5
        AnimeV45Curated,
        AnimeV45Full,
    }

    public enum ImageResolutionType
    {
        // Wallpaper
        WallpaperPortrait,
        WallpaperLandscape,

        // V1
        SmallPortrait,
        SmallLandscape,
        SmallSquare,

        NormalPortrait,
        NormalLandscape,
        NormalSquare,

        LargePortrait,
        LargeLandscape,
        LargeSquare,

        // V2
        SmallPortraitV2,
        SmallLandscapeV2,
        SmallSquareV2,

        NormalPortraitV2,
        NormalLandscapeV2,
        NormalSquareV2,

        LargePortraitV2,
        LargeLandscapeV2,
        LargeSquareV2,

        // V3
        SmallPortraitV3,
        SmallLandscapeV3,
        SmallSquareV3,

        NormalPortraitV3,
        NormalLandscapeV3,
        NormalSquareV3,

        LargePortraitV3,
        LargeLandscapeV3,
        LargeSquareV3,

        // V4
        SmallPortraitV4,
        SmallLandscapeV4,
        SmallSquareV4,

        NormalPortraitV4,
        NormalLandscapeV4,
        NormalSquareV4,

        LargePortraitV4,
        LargeLandscapeV4,
        LargeSquareV4,
    }

    public enum SamplerType
    {
        k_lms,
        k_euler,
        k_euler_ancestral,
        k_heun,
        plms,
        ddim,
        ddim_v3,
        nai_smea,
        nai_smea_dyn,
        k_dpmpp_2m,
        k_dpmpp_2m_sde,
        k_dpmpp_2s_ancestral,
        k_dpmpp_sde,
        k_dpm_2,
        k_dpm_2_ancestral,
        k_dpm_adaptive,
        k_dpm_fast,
    }

    public enum NegativePromptPresetType
    {
        LowQualityBadAnatomy,
        LowQuality,
        BadAnatomy,
        Heavy,
        Light,
        HumanFocus,
        FurryFocus,
        None,
    }

    public enum ReferenceType
    {
        Character,
        Style,
        CharacterAndStyle,
    }

    public enum UCPresetType
    {
        Heavy,
        Medium,
        Light,
        None,
    }

    public enum NoiseType
    {
        Native,
        Karras,
        Exponential,
        PolyExponential,
    }

    public enum ControlNetModelType
    {
        PaletteSwap,
        FormLock,
        Scribbler,
        BuildingControl,
        Landscaper,
    }

    public record ImageGenerateParameters
    {
        public int Width { get; init; } = 1024;
        public int Height { get; init; } = 1024;
        public SamplerType SamplerType { get; init; } = SamplerType.k_euler;
        public int Step { get; init; } = 28;
        public float Scale { get; init; } = 5.0f;
        public bool IsEnableSmea { get; init; } = false;
        public bool IsEnableSmeaDyn { get; init; } = false;
        public uint Seed { get; init; } = 0;
        public bool IsEnableAddQualityPrompt { get; init; } = false;
        public NegativePromptPresetType NegativePromptPreset { get; init; } = NegativePromptPresetType.None;
        public NoiseType Noise { get; init; } = NoiseType.Native;
        public ControlNetModelType? ControlNetModel { get; init; } = null;
        public float? ControlNetStrength { get; init; } = null;

        // NEW FEATURES
        public I2iParams? I2i { get; init; } = null;
        public UCPresetType UCPreset { get; init; } = UCPresetType.None;
        public List<CharacterReference> CharacterReferences { get; init; } = [];
        public List<Character> Characters { get; init; } = [];
        public ControlNet? ControlNetData { get; init; } = null;
        public int NumberOfSamples { get; init; } = 1;
        public bool EnableStreaming { get; init; } = false;
        public bool AddQualityTags { get; init; } = false;
    }

    /// <summary>Character positioning and prompt for multi-character generation</summary>
    public class Character
    {
        public string Prompt { get; set; } = string.Empty;
        public string UndesiredContent { get; set; } = string.Empty;
        public (double X, double Y) Position { get; set; } = (0.5, 0.5);
        public bool Enabled { get; set; } = true;
    }

    /// <summary>Character reference image for consistent character generation</summary>
    public class CharacterReference
    {
        public string Image { get; set; } = string.Empty;
        public ReferenceType Type { get; set; } = ReferenceType.Character;
        public float Fidelity { get; set; } = 0.75f;
        public bool Enabled { get; set; } = true;
    }

    /// <summary>Image-to-Image transformation parameters</summary>
    public class I2iParams
    {
        public string Image { get; set; } = string.Empty;
        public float Strength { get; set; } = 0.5f;
        public float Noise { get; set; } = 0.0f;
        public bool ColorCorrect { get; set; } = false;
        public bool AutoResize { get; set; } = true;
    }

    /// <summary>ControlNet image with vibe transfer settings</summary>
    public class ControlNetImage
    {
        public string Image { get; set; } = string.Empty;
        public float Strength { get; set; } = 0.5f;
        public bool Enabled { get; set; } = true;
    }

    /// <summary>ControlNet configuration (Vibe Transfer)</summary>
    public class ControlNet
    {
        public ControlNetModelType Model { get; set; } = ControlNetModelType.PaletteSwap;
        public List<ControlNetImage> Images { get; set; } = [];
    }

    /// <summary>Anlas cost estimate</summary>
    public class AnlasEstimate
    {
        public int BaseCost { get; set; }
        public int OpusCost { get; set; }
        public int TotalAnlas { get; set; }
        public int TotalAnlasOpus { get; set; }
        public Dictionary<string, int> CostBreakdown { get; set; } = [];
    }

    /// <summary>Streaming chunk from SSE response</summary>
    public class ImageStreamChunk
    {
        public string Image { get; set; } = string.Empty;
        public int SequenceNumber { get; set; }
        public int TotalChunks { get; set; }
    }

    /// <summary>V4 center point for character positioning</summary>
    public class CenterPoint(double x, double y)
    {
        public double X { get; set; } = x;
        public double Y { get; set; } = y;
    }

    /// <summary>V4 character caption with positioning</summary>
    public class V4CharacterCaption
    {
        public List<CenterPoint>? Centers { get; set; }
        public string? CharCaption { get; set; }
    }

    /// <summary>V4 caption structure for prompts</summary>
    public class V4Caption
    {
        public string? BaseCaption { get; set; }
        public List<V4CharacterCaption>? CharCaptions { get; set; }
    }

    /// <summary>V4 condition input for prompt/conditioning</summary>
    public class V4ConditionInput
    {
        public V4Caption? Caption { get; set; }
        public bool? LegacyUC { get; set; }
        public bool? UseCoords { get; set; }
        public bool? UseOrder { get; set; }
    }

    public enum ReferenceCaptionType
    {
        CharacterAndStyle,
        Character,
        Style,
    }

    /// <summary>Reference caption for image reference</summary>
    public class ReferenceCaption
    {
        public ReferenceCaptionType BaseCaption { get; set; } = ReferenceCaptionType.Character;
        public List<V4CharacterCaption> CharCaptions { get; set; } = [];
    }

    /// <summary>Reference condition input for character references</summary>
    public class ReferenceConditionInput
    {
        public ReferenceCaption? Caption { get; set; }
        public bool? LegacyUC { get; set; }
    }

    private static class AnlasCalculator
    {
        private const string QUALITY_TAGS = ", best quality, amazing quality, very aesthetic, absurdres";

        private static readonly Dictionary<UCPresetType, string> UC_PRESETS = new()
        {
            {
                UCPresetType.Heavy,
                "blurry, lowres, upscaled, artistic error, film grain, scan artifacts, worst quality, bad quality, jpeg artifacts, very displeasing, chromatic aberration, halftone, multiple views, logo, too many watermarks, negative space, blank page"
            },
            {
                UCPresetType.Medium,
                "blurry, lowres, upscaled, artistic error, scan artifacts, jpeg artifacts, logo, too many watermarks, negative space, blank page"
            },
            {
                UCPresetType.Light,
                "blurry, lowres, upscaled, logo, watermark, text"
            },
            { UCPresetType.None, "" },
        };

        public static AnlasEstimate CalculateAnlas(ImageGenerateParameters parameters, bool isOpus = false)
        {
            var estimate = new AnlasEstimate { CostBreakdown = [] };

            // Resolution multiplier
            int pixelCount = parameters.Width * parameters.Height;
            int resolutionCost = (pixelCount / (512 * 512)) * 5;
            if (pixelCount > 512 * 512 && pixelCount <= 1024 * 1024)
                resolutionCost = 5;
            else if (pixelCount > 1024 * 1024)
                resolutionCost = 10;

            // Steps cost
            int stepsCost = Math.Max(1, parameters.Step / 10);

            int baseCost = resolutionCost + stepsCost;

            // Sampler cost modifier
            int samplerCost = parameters.SamplerType switch
            {
                SamplerType.k_dpmpp_sde or SamplerType.k_dpmpp_2m_sde => 2,
                SamplerType.nai_smea or SamplerType.nai_smea_dyn => 1,
                _ => 0,
            };

            baseCost += samplerCost;

            // Add-ons cost
            int addonsCost = 0;

            if (parameters.IsEnableSmea || parameters.IsEnableSmeaDyn)
                addonsCost += 1;

            if (parameters.CharacterReferences.Count > 0)
                addonsCost += parameters.CharacterReferences.Count * 2;

            if (parameters.I2i != null)
                addonsCost += 3;

            if (parameters.ControlNetData?.Images.Count > 0)
                addonsCost += parameters.ControlNetData.Images.Count * 4;

            baseCost += addonsCost;

            baseCost *= parameters.NumberOfSamples;

            estimate.BaseCost = baseCost;
            estimate.TotalAnlas = baseCost;

            estimate.OpusCost = (int)(baseCost * 0.8);
            estimate.TotalAnlasOpus = isOpus ? estimate.OpusCost : estimate.TotalAnlas;

            estimate.CostBreakdown["Resolution"] = resolutionCost;
            estimate.CostBreakdown["Steps"] = stepsCost;
            estimate.CostBreakdown["Sampler"] = samplerCost;
            estimate.CostBreakdown["Add-ons"] = addonsCost;
            estimate.CostBreakdown["Samples"] = parameters.NumberOfSamples;

            return estimate;
        }

        public static string ApplyQualityTags(string prompt, bool addQuality)
        {
            if (addQuality)
                return prompt + QUALITY_TAGS;
            return prompt;
        }

        public static string GetUCPreset(UCPresetType preset)
        {
            return UC_PRESETS.TryGetValue(preset, out var text) ? text : "";
        }
    }

    private readonly Dictionary<ImageModelType, string> ImageModelName = new()
    {
        // V1
        { ImageModelType.AnimeCurated, "safe-diffusion" },
        { ImageModelType.AnimeFull, "nai-diffusion" },
        { ImageModelType.Furry, "nai-diffusion-furry" },

        // Inpainting V1
        { ImageModelType.InpaintingAnimeCurated, "safe-diffusion-inpainting" },
        { ImageModelType.InpaintingAnimeFull, "nai-diffusion-inpainting" },
        { ImageModelType.InpaintingFurry, "furry-diffusion-inpainting" },

        // V2
        { ImageModelType.AnimeV2, "nai-diffusion-2" },

        // V3
        { ImageModelType.AnimeV3, "nai-diffusion-3" },
        { ImageModelType.FurryV3, "nai-diffusion-furry-3" },

        // Inpainting V3
        { ImageModelType.InpaintingAnimeV3, "nai-diffusion-3-inpainting" },
        { ImageModelType.InpaintingFurryV3, "nai-diffusion-furry-3-inpainting" },

        // V4
        { ImageModelType.AnimeV4Preview, "nai-diffusion-4-curated-preview" },
        { ImageModelType.AnimeV4Full, "nai-diffusion-4-full" },

        // Inpainting V4
        { ImageModelType.InpaintingAnimeV4Curated, "nai-diffusion-4-curated-inpainting" },
        { ImageModelType.InpaintingAnimeV4Full, "nai-diffusion-4-full-inpainting" },

        // V4.5
        { ImageModelType.AnimeV45Curated, "nai-diffusion-4-5-curated" },
        { ImageModelType.AnimeV45Full, "nai-diffusion-4-5-full" },
    };

    private static string GenerateQualityIncludePrompt(ImageModelType imageModelType, string prompt)
    {
        return imageModelType switch
        {
            ImageModelType.AnimeCurated or ImageModelType.Furry or ImageModelType.AnimeFull or ImageModelType.InpaintingAnimeCurated or ImageModelType.InpaintingAnimeFull or ImageModelType.InpaintingFurry => $"masterpiece, best quality, {prompt}",
            ImageModelType.AnimeV2 => $"very aesthetic, best quality, absurdres, {prompt}",
            ImageModelType.AnimeV3 or ImageModelType.FurryV3 or ImageModelType.InpaintingAnimeV3 or ImageModelType.InpaintingFurryV3 => $"{prompt}, best quality, amazing quality, very aesthetic, absurdres",
            ImageModelType.AnimeV4Preview or ImageModelType.AnimeV4Full or ImageModelType.InpaintingAnimeV4Curated or ImageModelType.InpaintingAnimeV4Full or ImageModelType.AnimeV45Curated or ImageModelType.AnimeV45Full => prompt,
            _ => prompt,
        };
    }

    private static string GeneratePresetNegativePrompt(ImageModelType imageModelType, string prompt, NegativePromptPresetType negativePromptPresetType, string negativePrompt)
    {
        var negativePromptPreset = imageModelType switch
        {
            ImageModelType.AnimeCurated or ImageModelType.AnimeFull or ImageModelType.InpaintingAnimeCurated or ImageModelType.InpaintingAnimeFull => negativePromptPresetType switch
            {
                NegativePromptPresetType.LowQualityBadAnatomy => "nsfw, lowres, bad anatomy, bad hands, text, error, missing fingers, extra digit, fewer digits, cropped, worst quality, low quality, normal quality, jpeg artifacts, signature, watermark, username, blurry",
                NegativePromptPresetType.LowQuality => "nsfw, lowres, text, cropped, worst quality, low quality, normal quality, jpeg artifacts, signature, watermark, twitter username, blurry",
                NegativePromptPresetType.None => "lowres",
                _ => string.Empty,
            },

            ImageModelType.AnimeV2 => negativePromptPresetType switch
            {
                NegativePromptPresetType.Heavy => "nsfw, lowres, bad, text, error, missing, extra, fewer, cropped, jpeg artifacts, worst quality, bad quality, watermark, displeasing, unfinished, chromatic aberration, scan, scan artifacts",
                NegativePromptPresetType.Light => "nsfw, lowres, jpeg artifacts, worst quality, watermark, blurry, very displeasing",
                NegativePromptPresetType.None => "lowres",
                _ => string.Empty,
            },

            ImageModelType.AnimeV3 or ImageModelType.InpaintingAnimeV3 => negativePromptPresetType switch
            {
                NegativePromptPresetType.Heavy => "nsfw, lowres, {bad}, error, fewer, extra, missing, worst quality, jpeg artifacts, bad quality, watermark, unfinished, displeasing, chromatic aberration, signature, extra digits, artistic error, username, scan, [abstract]",
                NegativePromptPresetType.Light => "nsfw, lowres, jpeg artifacts, worst quality, watermark, blurry, very displeasing",
                NegativePromptPresetType.None => "lowres",
                _ => string.Empty,
            },

            ImageModelType.Furry or ImageModelType.InpaintingFurry => negativePromptPresetType switch
            {
                NegativePromptPresetType.LowQuality => "nsfw, worst quality, low quality, what has science done, what, nightmare fuel, eldritch horror, where is your god now, why",
                NegativePromptPresetType.BadAnatomy => "{worst quality}, low quality, distracting watermark, [nightmare fuel], {{unfinished}}, deformed, outline, pattern, simple background",
                NegativePromptPresetType.None => "low res",
                _ => string.Empty,
            },

            ImageModelType.FurryV3 or ImageModelType.InpaintingFurryV3 => negativePromptPresetType switch
            {
                NegativePromptPresetType.Heavy => "nsfw, {{worst quality}}, [displeasing], {unusual pupils}, guide lines, {{unfinished}}, {bad}, url, artist name, {{tall image}}, mosaic, {sketch page}, comic panel, impact (font), [dated], {logo}, ych, {what}, {where is your god now}, {distorted text}, repeated text, {floating head}, {1994}, {widescreen}, absolutely everyone, sequence, {compression artifacts}, hard translated, {cropped}, {commissioner name}, unknown text, high contrast",
                NegativePromptPresetType.Light => "{worst quality}, guide lines, unfinished, bad, url, tall image, widescreen, compression artifacts, unknown text",
                NegativePromptPresetType.None => "lowres",
                _ => string.Empty,
            },

            ImageModelType.AnimeV4Preview or ImageModelType.AnimeV4Full or ImageModelType.InpaintingAnimeV4Curated or ImageModelType.InpaintingAnimeV4Full => negativePromptPresetType switch
            {
                NegativePromptPresetType.Heavy => "blurry, lowres, upscaled, artistic error, film grain, scan artifacts, worst quality, bad quality, jpeg artifacts, very displeasing, chromatic aberration, halftone, multiple views, logo, too many watermarks, negative space, blank page",
                NegativePromptPresetType.Light => "blurry, lowres, upscaled, artistic error, scan artifacts, jpeg artifacts, logo, too many watermarks, negative space, blank page",
                NegativePromptPresetType.HumanFocus => "blurry, lowres, upscaled, artistic error, film grain, scan artifacts, bad anatomy, bad hands, worst quality, bad quality, jpeg artifacts, very displeasing, chromatic aberration, halftone, multiple views, logo, too many watermarks, @_@, mismatched pupils, glowing eyes, negative space, blank page",
                NegativePromptPresetType.None => "",
                _ => string.Empty,
            },

            ImageModelType.AnimeV45Curated or ImageModelType.AnimeV45Full => negativePromptPresetType switch
            {
                NegativePromptPresetType.Heavy => "blurry, lowres, upscaled, artistic error, film grain, scan artifacts, worst quality, bad quality, jpeg artifacts, very displeasing, chromatic aberration, halftone, multiple views, logo, too many watermarks, negative space, blank page",
                NegativePromptPresetType.Light => "blurry, lowres, upscaled, artistic error, scan artifacts, jpeg artifacts, logo, too many watermarks, negative space, blank page",
                NegativePromptPresetType.HumanFocus => "blurry, lowres, upscaled, artistic error, film grain, scan artifacts, bad anatomy, bad hands, worst quality, bad quality, jpeg artifacts, very displeasing, chromatic aberration, halftone, multiple views, logo, too many watermarks, @_@, mismatched pupils, glowing eyes, negative space, blank page, bad anatomy",
                NegativePromptPresetType.FurryFocus => "{worst quality}, distracting watermark, unfinished, bad quality, {widescreen}, upscale, {sequence}, {{grandfathered content}}, blurred foreground, chromatic aberration, sketch, everyone, [sketch background], simple, [flat colors], ych (character), outline, multiple scenes, [[horror (theme)]], comic",
                NegativePromptPresetType.None => "",
                _ => string.Empty,
            },

            _ => string.Empty,
        };

        var isIncludeNsfwPrompt = prompt.Contains("nsfw");

        if (isIncludeNsfwPrompt && negativePromptPreset.StartsWith("nsfw, "))
        {
            negativePromptPreset = negativePromptPreset[6..];
        }

        return (negativePrompt.Length == 0) ? negativePromptPreset : $"{negativePromptPreset}, {negativePrompt}";
    }

    private readonly Dictionary<SamplerType, string> SamplerName = new()
    {
        { SamplerType.k_lms, "k_lms" },
        { SamplerType.k_euler, "k_euler" },
        { SamplerType.k_euler_ancestral, "k_euler_ancestral" },
        { SamplerType.k_heun, "k_heun" },
        { SamplerType.plms, "plms" },
        { SamplerType.ddim, "ddim" },
        { SamplerType.ddim_v3, "ddim_v3" },
        { SamplerType.nai_smea, "nai_smea" },
        { SamplerType.nai_smea_dyn, "nai_smea_dyn" },
        { SamplerType.k_dpmpp_2m, "k_dpmpp_2m" },
        { SamplerType.k_dpmpp_2m_sde, "k_dpmpp_2m_sde" },
        { SamplerType.k_dpmpp_2s_ancestral, "k_dpmpp_2s_ancestral" },
        { SamplerType.k_dpmpp_sde, "k_dpmpp_sde" },
        { SamplerType.k_dpm_2, "k_dpm_2" },
        { SamplerType.k_dpm_2_ancestral, "k_dpm_2_ancestral" },
        { SamplerType.k_dpm_adaptive, "k_dpm_adaptive" },
        { SamplerType.k_dpm_fast, "k_dpm_fast" },
    };

    private readonly Dictionary<ControlNetModelType, string> ControlNetModelName = new()
    {
        { ControlNetModelType.PaletteSwap, "hed" },
        { ControlNetModelType.FormLock, "midas" },
        { ControlNetModelType.Scribbler, "fake_scribble" },
        { ControlNetModelType.BuildingControl, "mlsd" },
        { ControlNetModelType.Landscaper, "uniformer" },
    };

    private static string ToJson(Action<Utf8JsonWriter> action)
    {
        var arrayBufferWriter = new ArrayBufferWriter<byte>();

        using (var utf8JsonWriter = new Utf8JsonWriter(arrayBufferWriter, jsonWriterOptions))
        {
            utf8JsonWriter.WriteStartObject();

            action(utf8JsonWriter);

            utf8JsonWriter.WriteEndObject();
        }

        return Encoding.UTF8.GetString(arrayBufferWriter.WrittenSpan);
    }

    private async Task<HttpResponseMessage> CallApiAsync(HttpMethod httpMethod, string url, Action<Utf8JsonWriter> action)
    {
        var request = new HttpRequestMessage(httpMethod, url);

        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", ApiKey);

        request.Content = new StringContent(ToJson(action), Encoding.UTF8, "application/json");

        using var httpClient = httpClientFactory.CreateClient();

        return await httpClient.SendAsync(request);
    }

    public static (int width, int height) GetImageResolutionPixel(ImageResolutionType imageResolutionType)
    {
        return imageResolutionType switch
        {
            // Wallpaper
            ImageResolutionType.WallpaperPortrait => (1088, 1920),
            ImageResolutionType.WallpaperLandscape => (1920, 1088),

            // V1
            ImageResolutionType.SmallPortrait => (384, 640),
            ImageResolutionType.SmallLandscape => (640, 384),
            ImageResolutionType.SmallSquare => (512, 512),
            ImageResolutionType.NormalPortrait => (512, 768),
            ImageResolutionType.NormalLandscape => (768, 512),
            ImageResolutionType.NormalSquare => (640, 640),
            ImageResolutionType.LargePortrait => (512, 1024),
            ImageResolutionType.LargeLandscape => (1024, 512),
            ImageResolutionType.LargeSquare => (1024, 1024),

            // V2
            ImageResolutionType.SmallPortraitV2 => (512, 768),
            ImageResolutionType.SmallLandscapeV2 => (768, 512),
            ImageResolutionType.SmallSquareV2 => (640, 640),
            ImageResolutionType.NormalPortraitV2 => (832, 1216),
            ImageResolutionType.NormalLandscapeV2 => (1216, 832),
            ImageResolutionType.NormalSquareV2 => (1024, 1024),
            ImageResolutionType.LargePortraitV2 => (1024, 1536),
            ImageResolutionType.LargeLandscapeV2 => (1536, 1024),
            ImageResolutionType.LargeSquareV2 => (1472, 1472),

            // V3
            ImageResolutionType.SmallPortraitV3 => (512, 768),
            ImageResolutionType.SmallLandscapeV3 => (768, 512),
            ImageResolutionType.SmallSquareV3 => (640, 640),
            ImageResolutionType.NormalPortraitV3 => (832, 1216),
            ImageResolutionType.NormalLandscapeV3 => (1216, 832),
            ImageResolutionType.NormalSquareV3 => (1024, 1024),
            ImageResolutionType.LargePortraitV3 => (1024, 1536),
            ImageResolutionType.LargeLandscapeV3 => (1536, 1024),
            ImageResolutionType.LargeSquareV3 => (1472, 1472),

            // V4
            ImageResolutionType.SmallPortraitV4 => (512, 768),
            ImageResolutionType.SmallLandscapeV4 => (768, 512),
            ImageResolutionType.SmallSquareV4 => (640, 640),
            ImageResolutionType.NormalPortraitV4 => (832, 1216),
            ImageResolutionType.NormalLandscapeV4 => (1216, 832),
            ImageResolutionType.NormalSquareV4 => (1024, 1024),
            ImageResolutionType.LargePortraitV4 => (1024, 1536),
            ImageResolutionType.LargeLandscapeV4 => (1536, 1024),
            ImageResolutionType.LargeSquareV4 => (1472, 1472),

            _ => (0, 0)
        };
    }

    public static uint GetRandomSeed()
    {
        return (uint)(Random.Shared.NextInt64(uint.MaxValue) + 1);
    }

    public void SetApiKey(string apiKey)
    {
        ApiKey = apiKey;
    }

    public async Task<HttpResponseMessage> GenerateImageAsync(ImageModelType imageModelType, string prompt, string negativePrompt, ImageGenerateParameters parameters)
    {
        // Build the full prompt with quality tags if enabled
        string finalPrompt = parameters.AddQualityTags
            ? AnlasCalculator.ApplyQualityTags(prompt, true)
            : prompt;

        // For backward compatibility, also use the legacy flag
        if (parameters.IsEnableAddQualityPrompt && !parameters.AddQualityTags)
        {
            finalPrompt = GenerateQualityIncludePrompt(imageModelType, prompt);
        }

        // Determine if this is a V4 model
        bool isV4Model = imageModelType is ImageModelType.AnimeV4Preview or ImageModelType.AnimeV4Full
            or ImageModelType.InpaintingAnimeV4Curated or ImageModelType.InpaintingAnimeV4Full
            or ImageModelType.AnimeV45Curated or ImageModelType.AnimeV45Full;

        return await CallApiAsync(HttpMethod.Post, GenerateImageUrl, (utf8JsonWriter) =>
        {
            // Main request object
            if (isV4Model && (parameters.Characters.Count > 0 || parameters.CharacterReferences.Count > 0))
            {
                // V4 with character prompts - use caption-based structure
                WriteV4Request(utf8JsonWriter, imageModelType, finalPrompt, negativePrompt, parameters);
            }
            else
            {
                // Legacy/standard request structure
                WriteStandardRequest(utf8JsonWriter, imageModelType, finalPrompt, negativePrompt, parameters);
            }
        });
    }

    private void WriteStandardRequest(Utf8JsonWriter utf8JsonWriter, ImageModelType imageModelType, string prompt, string negativePrompt, ImageGenerateParameters parameters)
    {
        utf8JsonWriter.WriteString("input", prompt);
        utf8JsonWriter.WriteString("model", ImageModelName[imageModelType]);
        utf8JsonWriter.WriteString("action", "generate");
        utf8JsonWriter.WriteStartObject("parameters");

        utf8JsonWriter.WriteString("width", parameters.Width.ToString());
        utf8JsonWriter.WriteString("height", parameters.Height.ToString());
        utf8JsonWriter.WriteString("scale", parameters.Scale.ToString());
        utf8JsonWriter.WriteString("sampler", SamplerName[parameters.SamplerType]);
        utf8JsonWriter.WriteString("steps", parameters.Step.ToString());
        utf8JsonWriter.WriteString("sm", parameters.IsEnableSmea.ToString().ToLower());
        utf8JsonWriter.WriteString("sm_dyn", parameters.IsEnableSmeaDyn.ToString().ToLower());
        utf8JsonWriter.WriteString("noise_schedule", parameters.Noise.ToString().ToLower());
        utf8JsonWriter.WriteString("seed", parameters.Seed.ToString());

        // Build negative prompt
        string finalNegativePrompt = negativePrompt;
        if (parameters.UCPreset != UCPresetType.None)
        {
            string ucText = AnlasCalculator.GetUCPreset(parameters.UCPreset);
            finalNegativePrompt = string.IsNullOrEmpty(negativePrompt) ? ucText : $"{ucText}, {negativePrompt}";
        }
        else if (parameters.NegativePromptPreset != NegativePromptPresetType.None)
        {
            finalNegativePrompt = GeneratePresetNegativePrompt(imageModelType, prompt, parameters.NegativePromptPreset, negativePrompt);
        }

        utf8JsonWriter.WriteString("negative_prompt", finalNegativePrompt);

        // I2I parameters
        if (parameters.I2i != null)
        {
            utf8JsonWriter.WriteString("strength", parameters.I2i.Strength.ToString());
            utf8JsonWriter.WriteString("noise", parameters.I2i.Noise.ToString());
            utf8JsonWriter.WriteString("extra_noise_seed", parameters.I2i.Noise.ToString());
        }

        // ControlNet
        if (parameters.ControlNetModel.HasValue)
        {
            utf8JsonWriter.WriteString("control_net_model", ControlNetModelName[parameters.ControlNetModel.Value]);
        }

        if (parameters.ControlNetStrength.HasValue)
        {
            utf8JsonWriter.WriteString("control_net_strength", parameters.ControlNetStrength.Value.ToString());
        }

        // Number of samples
        if (parameters.NumberOfSamples > 1)
        {
            utf8JsonWriter.WriteString("n_samples", parameters.NumberOfSamples.ToString());
        }

        utf8JsonWriter.WriteEndObject();
    }

    private void WriteV4Request(Utf8JsonWriter utf8JsonWriter, ImageModelType imageModelType, string prompt, string negativePrompt, ImageGenerateParameters parameters)
    {
        // V4 uses caption-based structure for character prompts
        utf8JsonWriter.WriteString("model", ImageModelName[imageModelType]);
        utf8JsonWriter.WriteString("action", "generate");

        // Build caption JSON string manually to avoid JsonSerializer.Serialize warning
        string captionJson = BuildV4CaptionJson(prompt, parameters.Characters);
        utf8JsonWriter.WriteString("input", captionJson);

        utf8JsonWriter.WriteStartObject("parameters");
        utf8JsonWriter.WriteString("width", parameters.Width.ToString());
        utf8JsonWriter.WriteString("height", parameters.Height.ToString());
        utf8JsonWriter.WriteString("scale", parameters.Scale.ToString());
        utf8JsonWriter.WriteString("sampler", SamplerName[parameters.SamplerType]);
        utf8JsonWriter.WriteString("steps", parameters.Step.ToString());
        utf8JsonWriter.WriteString("sm", parameters.IsEnableSmea.ToString().ToLower());
        utf8JsonWriter.WriteString("sm_dyn", parameters.IsEnableSmeaDyn.ToString().ToLower());
        utf8JsonWriter.WriteString("noise_schedule", parameters.Noise.ToString().ToLower());
        utf8JsonWriter.WriteString("seed", parameters.Seed.ToString());

        // Build negative prompt (UC)
        string finalUC = negativePrompt;
        if (parameters.UCPreset != UCPresetType.None)
        {
            string ucText = AnlasCalculator.GetUCPreset(parameters.UCPreset);
            finalUC = string.IsNullOrEmpty(negativePrompt) ? ucText : $"{ucText}, {negativePrompt}";
        }

        utf8JsonWriter.WriteString("negative_prompt", finalUC);

        utf8JsonWriter.WriteEndObject();
    }

    private static string BuildV4CaptionJson(string basePrompt, List<Character> characters)
    {
        var sb = new StringBuilder();
        sb.Append("{\"base_caption\":");
        EscapeJsonString(sb, basePrompt);

        if (characters.Count > 0)
        {
            sb.Append(",\"char_captions\":[");
            for (int i = 0; i < characters.Count; i++)
            {
                if (i > 0) sb.Append(',');
                var character = characters[i];
                sb.Append("{\"char_caption\":");
                EscapeJsonString(sb, character.Prompt);
                sb.Append(",\"centers\":[{\"x\":");
                sb.Append(character.Position.X.ToString(System.Globalization.CultureInfo.InvariantCulture));
                sb.Append(",\"y\":");
                sb.Append(character.Position.Y.ToString(System.Globalization.CultureInfo.InvariantCulture));
                sb.Append("}]}");
            }
            sb.Append(']');
        }

        sb.Append('}');
        return sb.ToString();
    }

    private static void EscapeJsonString(StringBuilder sb, string value)
    {
        sb.Append('"');
        foreach (char c in value)
        {
            switch (c)
            {
                case '"':
                    sb.Append("\\\"");
                    break;
                case '\\':
                    sb.Append("\\\\");
                    break;
                case '\b':
                    sb.Append("\\b");
                    break;
                case '\f':
                    sb.Append("\\f");
                    break;
                case '\n':
                    sb.Append("\\n");
                    break;
                case '\r':
                    sb.Append("\\r");
                    break;
                case '\t':
                    sb.Append("\\t");
                    break;
                default:
                    if (c < 32 || (c >= 127 && c < 160))
                    {
                        sb.Append($"\\u{(int)c:X4}");
                    }
                    else
                    {
                        sb.Append(c);
                    }
                    break;
            }
        }
        sb.Append('"');
    }

    /// <summary>Generate multiple images in batch.</summary>
    public async Task<List<HttpResponseMessage>> GenerateImageBatchAsync(
        ImageModelType imageModelType,
        string prompt,
        string negativePrompt,
        ImageGenerateParameters parameters,
        int batchCount)
    {
        var results = new List<HttpResponseMessage>();
        var batchParameters = parameters with { NumberOfSamples = 1 };

        for (int i = 0; i < batchCount; i++)
        {
            var response = await GenerateImageAsync(imageModelType, prompt, negativePrompt, batchParameters);
            results.Add(response);
        }

        return results;
    }

    /// <summary>Generate image with SSE streaming for real-time progress.</summary>
    public async IAsyncEnumerable<ImageStreamChunk> GenerateImageStreamAsync(
        ImageModelType imageModelType,
        string prompt,
        string negativePrompt,
        ImageGenerateParameters parameters)
    {
        var streamParameters = parameters with { EnableStreaming = true };
        using var response = await GenerateImageAsync(imageModelType, prompt, negativePrompt, streamParameters);

        if (!response.IsSuccessStatusCode)
        {
            yield break;
        }

        using var stream = await response.Content.ReadAsStreamAsync();
        using var reader = new StreamReader(stream);

        string? line;
        int chunkNumber = 0;
        int totalChunks = 0;

        while ((line = await reader.ReadLineAsync()) != null)
        {
            if (string.IsNullOrWhiteSpace(line))
                continue;

            // Parse SSE format: "data: {...}"
            if (line.StartsWith("data: "))
            {
                string jsonData = line[6..];

                ImageStreamChunk? chunk = null;
                try
                {
                    using var doc = JsonDocument.Parse(jsonData);
                    var root = doc.RootElement;

                    if (root.TryGetProperty("image", out var imageProp))
                    {
                        chunk = new ImageStreamChunk
                        {
                            Image = imageProp.GetString() ?? string.Empty,
                            SequenceNumber = chunkNumber++,
                            TotalChunks = totalChunks > 0 ? totalChunks : -1
                        };

                        if (root.TryGetProperty("total_chunks", out var totalProp))
                        {
                            totalChunks = totalProp.GetInt32();
                            chunk.TotalChunks = totalChunks;
                        }
                    }
                }
                catch
                {
                    // Skip malformed chunks
                }

                if (chunk != null)
                {
                    yield return chunk;
                }
            }
        }
    }

    /// <summary>Estimate the Anlas cost for given parameters.</summary>
    public static Task<AnlasEstimate> EstimateAnlasAsync(
        ImageGenerateParameters parameters,
        bool isOpus = false)
    {
        var estimate = AnlasCalculator.CalculateAnlas(parameters, isOpus);
        return Task.FromResult(estimate);
    }
}
