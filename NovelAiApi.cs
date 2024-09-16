using System;
using System.Buffers;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace NovelAI_API
{
    public class NovelAiApi(IHttpClientFactory httpClientFactory)
    {
        private const string BaseAddress = "https://api.novelai.net";
        private const string GenerateImageUrl = BaseAddress + "/ai/generate-image";

        public const int RandomSeedValue = -1;

        private string ApiKey { get; set; } = string.Empty;

        private static readonly JsonWriterOptions jsonWriterOptions = new() { Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping, Indented = false };

        public enum ImageModelType
        {
            AnimeCurated,
            AnimeFull,
            AnimeV2,
            AnimeV3,
            Furry,
            FurryV3,
            InpaintingAnimeCurated,
            InpaintingAnimeFull,
            InpaintingAnimeV3,
            InpaintingFurry,
            InpaintingFurryV3,
        }

        public enum ImageResolutionType
        {
            SmallPortrait,
            SmallLandscape,
            SmallSquare,

            NormalPortrait,
            NormalLandscape,
            NormalSquare,

            LargePortrait,
            LargeLandscape,
            LargeSquare,

            SmallPortraitV2,
            SmallLandscapeV2,
            SmallSquareV2,

            NormalPortraitV2,
            NormalLandscapeV2,
            NormalSquareV2,

            LargePortraitV2,
            LargeLandscapeV2,
            LargeSquareV2,

            SmallPortraitV3,
            SmallLandscapeV3,
            SmallSquareV3,

            NormalPortraitV3,
            NormalLandscapeV3,
            NormalSquareV3,

            LargePortraitV3,
            LargeLandscapeV3,
            LargeSquareV3,

            WallpaperPortrait,
            WallpaperLandscape,
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
            None,
        }

        public enum NoiseType
        {
            Native,
            Karras,
            Exponential,
            PolyExponential,
        }

        public class ImageGenerateParameters
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
        }

        private readonly Dictionary<ImageModelType, string> ImageModelName = new()
        {
            { ImageModelType.AnimeCurated, "safe-diffusion" },
            { ImageModelType.AnimeFull, "nai-diffusion" },
            { ImageModelType.AnimeV2, "nai-diffusion-2" },
            { ImageModelType.AnimeV3, "nai-diffusion-3" },
            { ImageModelType.Furry, "nai-diffusion-furry" },
            { ImageModelType.FurryV3, "nai-diffusion-furry-3" },
            { ImageModelType.InpaintingAnimeCurated, "safe-diffusion-inpainting" },
            { ImageModelType.InpaintingAnimeFull, "nai-diffusion-inpainting" },
            { ImageModelType.InpaintingFurry, "furry-diffusion-inpainting" },
            { ImageModelType.InpaintingAnimeV3, "nai-diffusion-3-inpainting" },
            { ImageModelType.InpaintingFurryV3, "nai-diffusion-furry-3-inpainting" },
        };

        private static string GenerateQualityIncludePrompt(ImageModelType imageModelType, string prompt)
        {
            return imageModelType switch
            {
                ImageModelType.AnimeCurated or ImageModelType.Furry or ImageModelType.AnimeFull or ImageModelType.InpaintingAnimeCurated or ImageModelType.InpaintingAnimeFull or ImageModelType.InpaintingFurry => $"masterpiece, best quality, {prompt}",
                ImageModelType.AnimeV2 => $"very aesthetic, best quality, absurdres, {prompt}",
                ImageModelType.AnimeV3 or ImageModelType.FurryV3 or ImageModelType.InpaintingAnimeV3 or ImageModelType.InpaintingFurryV3 => $"{prompt}, best quality, amazing quality, very aesthetic, absurdres",
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

                _ => string.Empty,
            };

            var isIncludeNsfwPrompt = prompt.Contains("nsfw");

            if (isIncludeNsfwPrompt && negativePromptPreset.StartsWith("nsfw, "))
            {
                negativePromptPreset = negativePromptPreset[6..];
            }

            return (negativePrompt.Length == 0) ? negativePromptPreset : $"{negativePromptPreset}, {negativePrompt}";
        }

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

        private async Task<HttpResponseMessage> CallApiAsync(HttpMethod httpMethod, string requestUrl, Action<Utf8JsonWriter> action)
        {
            var request = new HttpRequestMessage(httpMethod, requestUrl);

            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", ApiKey);

            request.Content = new StringContent(ToJson(action), Encoding.UTF8, "application/json");

            using var httpClient = httpClientFactory.CreateClient();

            return await httpClient.SendAsync(request);
        }

        public static (int width, int height) GetImageResolutionPixel(ImageResolutionType imageResolutionType)
        {
            return imageResolutionType switch
            {
                ImageResolutionType.SmallPortrait => (384, 640),
                ImageResolutionType.SmallLandscape => (640, 384),
                ImageResolutionType.SmallSquare => (512, 512),
                ImageResolutionType.NormalPortrait => (512, 768),
                ImageResolutionType.NormalLandscape => (768, 512),
                ImageResolutionType.NormalSquare => (640, 640),
                ImageResolutionType.LargePortrait => (512, 1024),
                ImageResolutionType.LargeLandscape => (1024, 512),
                ImageResolutionType.LargeSquare => (1024, 1024),
                ImageResolutionType.SmallPortraitV2 => (512, 768),
                ImageResolutionType.SmallLandscapeV2 => (768, 512),
                ImageResolutionType.SmallSquareV2 => (640, 640),
                ImageResolutionType.NormalPortraitV2 => (832, 1216),
                ImageResolutionType.NormalLandscapeV2 => (1216, 832),
                ImageResolutionType.NormalSquareV2 => (1024, 1024),
                ImageResolutionType.LargePortraitV2 => (1024, 1536),
                ImageResolutionType.LargeLandscapeV2 => (1536, 1024),
                ImageResolutionType.LargeSquareV2 => (1472, 1472),
                ImageResolutionType.SmallPortraitV3 => (512, 768),
                ImageResolutionType.SmallLandscapeV3 => (768, 512),
                ImageResolutionType.SmallSquareV3 => (640, 640),
                ImageResolutionType.NormalPortraitV3 => (832, 1216),
                ImageResolutionType.NormalLandscapeV3 => (1216, 832),
                ImageResolutionType.NormalSquareV3 => (1024, 1024),
                ImageResolutionType.LargePortraitV3 => (1024, 1536),
                ImageResolutionType.LargeLandscapeV3 => (1536, 1024),
                ImageResolutionType.LargeSquareV3 => (1472, 1472),
                ImageResolutionType.WallpaperPortrait => (1088, 1920),
                ImageResolutionType.WallpaperLandscape => (1920, 1088),
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
            return await CallApiAsync(HttpMethod.Post, GenerateImageUrl, (utf8JsonWriter) =>
            {
                utf8JsonWriter.WriteString("input", (parameters.IsEnableAddQualityPrompt) ? GenerateQualityIncludePrompt(imageModelType, prompt) : prompt);
                utf8JsonWriter.WriteString("model", ImageModelName[imageModelType]);
                utf8JsonWriter.WriteString("action", "generate");
                utf8JsonWriter.WriteStartObject("parameters");
                utf8JsonWriter.WriteString("width", parameters.Width.ToString());
                utf8JsonWriter.WriteString("height", parameters.Height.ToString());
                utf8JsonWriter.WriteString("scale", parameters.Scale.ToString());
                utf8JsonWriter.WriteString("sampler", parameters.SamplerType.ToString());
                utf8JsonWriter.WriteString("steps", parameters.Step.ToString());
                utf8JsonWriter.WriteString("sm", parameters.IsEnableSmea.ToString().ToLower());
                utf8JsonWriter.WriteString("sm_dyn", parameters.IsEnableSmeaDyn.ToString().ToLower());
                utf8JsonWriter.WriteString("noise_schedule", parameters.Noise.ToString().ToLower());
                utf8JsonWriter.WriteString("seed", parameters.Seed.ToString());
                utf8JsonWriter.WriteString("negative_prompt", GeneratePresetNegativePrompt(imageModelType, prompt, parameters.NegativePromptPreset, negativePrompt));
                utf8JsonWriter.WriteEndObject();
            });
        }
    }
}
