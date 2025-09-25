using Newtonsoft.Json.Linq;

namespace TagbooruQuest.Services.ComfyUI;

public interface IPromptJsonMapper
{
    Task<JObject> MapPromptToWorkflowAsync(PromptParts prompt, IComfySettingsService settings);
}

public class PromptJsonMapper : IPromptJsonMapper
{
    private readonly string _workflowTemplatePath;
    private JObject? _workflowTemplate;

    public PromptJsonMapper()
    {
        _workflowTemplatePath = Path.Combine(FileSystem.AppDataDirectory, "comfyui-workflow.json");
    }

    public async Task<JObject> MapPromptToWorkflowAsync(PromptParts prompt, IComfySettingsService settings)
    {
        // Load template if not cached
        if (_workflowTemplate == null)
        {
            await LoadWorkflowTemplateAsync();
        }

        // Clone the template
        var workflow = (JObject)_workflowTemplate!.DeepClone();

        // Map settings to workflow nodes
        MapCheckpoint(workflow, settings.SelectedCheckpoint ?? "illustriousXL_v01.safetensors");
        MapPositivePrompt(workflow, prompt.Positive);
        MapNegativePrompt(workflow, prompt.Negative);
        MapImageSize(workflow, settings.Width, settings.Height);
        MapSamplerSettings(workflow, settings);

        return workflow;
    }

    private async Task LoadWorkflowTemplateAsync()
    {
        // First try to copy from bundle to app data if not exists
        if (!File.Exists(_workflowTemplatePath))
        {
            try
            {
                using var stream = await FileSystem.OpenAppPackageFileAsync("comfyui-workflow.json");
                using var outFile = File.Create(_workflowTemplatePath);
                await stream.CopyToAsync(outFile);
            }
            catch
            {
                // If copying fails, create a minimal default workflow
                await CreateDefaultWorkflowAsync();
                return;
            }
        }

        // Load the template
        var json = await File.ReadAllTextAsync(_workflowTemplatePath);
        _workflowTemplate = JObject.Parse(json);
    }

    private async Task CreateDefaultWorkflowAsync()
    {
        var defaultWorkflow = JObject.Parse(@"{
  ""3"": {
    ""inputs"": {
      ""seed"": 156680208700286,
      ""steps"": 20,
      ""cfg"": 8.0,
      ""sampler_name"": ""euler"",
      ""scheduler"": ""normal"",
      ""denoise"": 1,
      ""model"": [""4"", 0],
      ""positive"": [""6"", 0],
      ""negative"": [""7"", 0],
      ""latent_image"": [""5"", 0]
    },
    ""class_type"": ""KSampler""
  },
  ""4"": {
    ""inputs"": {
      ""ckpt_name"": ""illustriousXL_v01.safetensors""
    },
    ""class_type"": ""CheckpointLoaderSimple""
  },
  ""5"": {
    ""inputs"": {
      ""width"": 1024,
      ""height"": 1024,
      ""batch_size"": 1
    },
    ""class_type"": ""EmptyLatentImage""
  },
  ""6"": {
    ""inputs"": {
      ""text"": ""((tag)), white background, anime coloring, masterpiece,best quality"",
      ""clip"": [""4"", 1]
    },
    ""class_type"": ""CLIPTextEncode""
  },
  ""7"": {
    ""inputs"": {
      ""text"": ""lowres, bad anatomy, bad hands, text, error, missing fingers, extra digit, fewer digits, cropped, worst quality, low quality, normal quality, jpeg artifacts, signature, watermark, username, blurry"",
      ""clip"": [""4"", 1]
    },
    ""class_type"": ""CLIPTextEncode""
  },
  ""8"": {
    ""inputs"": {
      ""samples"": [""3"", 0],
      ""vae"": [""4"", 2]
    },
    ""class_type"": ""VAEDecode""
  },
  ""9"": {
    ""inputs"": {
      ""filename_prefix"": ""ComfyUI"",
      ""images"": [""8"", 0]
    },
    ""class_type"": ""SaveImage""
  }
}");

        await File.WriteAllTextAsync(_workflowTemplatePath, defaultWorkflow.ToString());
        _workflowTemplate = defaultWorkflow;
    }

    private static void MapCheckpoint(JObject workflow, string checkpointName)
    {
        SetNodeInput(workflow, "4", "ckpt_name", checkpointName);
    }

    private static void MapPositivePrompt(JObject workflow, string positivePrompt)
    {
        // Replace ((tag)) placeholder with actual prompt
        var template = "((tag)), full body, straight-on, white background, anime coloring, masterpiece,best quality";
        var finalPrompt = template.Replace("((tag))", positivePrompt);
        SetNodeInput(workflow, "6", "text", finalPrompt);
    }

    private static void MapNegativePrompt(JObject workflow, string negativePrompt)
    {
        var defaultNegative = "lowres, bad anatomy, bad hands, text, error, missing fingers, extra digit, fewer digits, cropped, worst quality, low quality, normal quality, jpeg artifacts, signature, watermark, username, blurry";
        var finalNegative = string.IsNullOrEmpty(negativePrompt) ? defaultNegative : negativePrompt;
        SetNodeInput(workflow, "7", "text", finalNegative);
    }

    private static void MapImageSize(JObject workflow, int width, int height)
    {
        SetNodeInput(workflow, "5", "width", width);
        SetNodeInput(workflow, "5", "height", height);
    }

    private static void MapSamplerSettings(JObject workflow, IComfySettingsService settings)
    {
        SetNodeInput(workflow, "3", "seed", settings.Seed);
        SetNodeInput(workflow, "3", "steps", settings.Steps);
        SetNodeInput(workflow, "3", "cfg", settings.Cfg);
        SetNodeInput(workflow, "3", "sampler_name", settings.SamplerName);
        SetNodeInput(workflow, "3", "scheduler", settings.Scheduler);
        SetNodeInput(workflow, "3", "denoise", settings.Denoise);
    }

    private static void SetNodeInput(JObject workflow, string nodeId, string inputKey, object value)
    {
        var node = workflow[nodeId];
        if (node?["inputs"] is JObject inputs)
        {
            inputs[inputKey] = JToken.FromObject(value);
        }
    }
}