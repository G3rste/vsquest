using Cake.Common;
using Cake.Common.IO;
using Cake.Common.Tools.DotNet;
using Cake.Common.Tools.DotNet.Clean;
using Cake.Common.Tools.DotNet.Publish;
using Cake.Core;
using Cake.Core.Diagnostics;
using Cake.Frosting;
using Cake.Json;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using Vintagestory.API.Common;

namespace CakeBuild
{
	public static class Program
	{
		public static int Main(string[] args)
		{
			return new CakeHost()
				.UseContext<BuildContext>()
				.Run(args);
		}
	}

	public class BuildContext : FrostingContext
	{
		public const string ProjectName = "vsquest";
        public const string ExampleModInfoPath = "../example/modinfo.json";
        public const string ModInfoPath = $"../{ProjectName}/resources/modinfo.json";
		public const string ModIconPath = $"../{ProjectName}/resources/modicon.png";
        public const string AssetsPath = $"../{ProjectName}/resources/assets";

        public string BuildConfiguration { get; }
		public string Version { get; }
		public string GameVersion { get; }
		public string Name { get; }
		public bool SkipJsonValidation { get; }

		public BuildContext(ICakeContext context)
			: base(context)
		{
			BuildConfiguration = context.Argument("configuration", "Release");
			SkipJsonValidation = context.Argument("skipJsonValidation", false);

			var modInfo = context.DeserializeJsonFromFile<ModInfo>(ModInfoPath);
            Name = modInfo.ModID;
            Version = modInfo.Version;
            GameVersion = modInfo.Dependencies.First(d => d.ModID.Equals("game", StringComparison.InvariantCultureIgnoreCase)).Version;
		}
	}

	[TaskName("ValidateJson")]
	public sealed class ValidateJsonTask : FrostingTask<BuildContext>
	{
		public override void Run(BuildContext context)
		{
			if (context.SkipJsonValidation)
			{
				return;
			}
			var jsonFiles = context.GetFiles($"{BuildContext.AssetsPath}/**/*.json").Concat(context.GetFiles("../example/assets/**/*.json"));
			foreach (var file in jsonFiles)
			{
				try
				{
					var json = File.ReadAllText(file.FullPath);
					JToken.Parse(json);
				}
				catch (JsonException ex)
				{
					throw new Exception($"Validation failed for JSON file: {file.FullPath}{Environment.NewLine}{ex.Message}", ex);
				}
			}
		}
	}

    [TaskName("PackExemple")]
    [IsDependentOn(typeof(ValidateJsonTask))]
    public sealed class PackExempleTask : FrostingTask<BuildContext>
    {
		public override void Run(BuildContext context)
		{
			List<ModDependency> dependencies = [new ModDependency("game", context.GameVersion), new ModDependency(context.Name, context.Version)];

			var modinfo = context.DeserializeJsonFromFile<ModInfo>(BuildContext.ExampleModInfoPath);
			modinfo.Version = context.Version;
			modinfo.Dependencies = dependencies.AsReadOnly();

			context.SerializeJsonToPrettyFile(BuildContext.ExampleModInfoPath, modinfo);

            context.EnsureDirectoryExists("../Releases");
            context.CleanDirectory("../Releases");
            context.Zip($"../example", $"../Releases/{context.Name}example_{context.Version}.zip");
        }
    }

    [TaskName("Build")]
	[IsDependentOn(typeof(PackExempleTask))]
	public sealed class BuildTask : FrostingTask<BuildContext>
	{
		public override void Run(BuildContext context)
		{
			context.DotNetClean($"../{BuildContext.ProjectName}/{BuildContext.ProjectName}.csproj",
				new DotNetCleanSettings
				{
					Configuration = context.BuildConfiguration
				});

			
			context.DotNetPublish($"../{BuildContext.ProjectName}/{BuildContext.ProjectName}.csproj",
				new DotNetPublishSettings
				{
					Configuration = context.BuildConfiguration
				});
		}
	}

    [TaskName("RetrieveLang")]
    [IsDependentOn(typeof(BuildTask))]
    public sealed class RetrieveLangTask : FrostingTask<BuildContext>
    {
        public override void Run(BuildContext context)
        {
			var url = $"https://dl.dropboxusercontent.com/scl/fo/q7u3idxz3edsytki8n6m4/h/{context.Name}-g3rste.zip?dl=1&rlkey=mc3xn22a49qwrjp5cmx1he0ay";
			var filePath = $"../Releases/test.zip";
            var directoryPath = $"{BuildContext.AssetsPath}/{context.Name}/lang";

            using var httpClient = new HttpClient
            {
                Timeout = TimeSpan.FromSeconds(30)
            };

            try
			{
				using (var response = httpClient.GetAsync(url).GetAwaiter().GetResult())
				{
                    response.EnsureSuccessStatusCode();

                    using var contentStream = response.Content.ReadAsStream();
                    using var fileStream = new FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.None);

                    contentStream.CopyTo(fileStream);
                }
				
				context.Unzip(filePath, directoryPath, true);
				context.DeleteFile(filePath);
			}
			catch (Exception ex)
			{
				context.Log.Error(ex.Message);
			}
        }
    }

    [TaskName("Package")]
	[IsDependentOn(typeof(RetrieveLangTask))]
	public sealed class PackageTask : FrostingTask<BuildContext>
	{
		public override void Run(BuildContext context)
		{
			var modDir = $"../Releases/{context.Name}";

            context.EnsureDirectoryExists(modDir);
			context.CopyFiles($"../{BuildContext.ProjectName}/bin/{context.BuildConfiguration}/Mods/mod/publish/*", modDir);
			if (context.DirectoryExists(BuildContext.AssetsPath))
			{
				context.CopyDirectory(BuildContext.AssetsPath, $"{modDir}/assets");
			}
			context.CopyFile(BuildContext.ModInfoPath, $"{modDir}/modinfo.json");
			if (context.FileExists(BuildContext.ModIconPath))
			{
				context.CopyFile(BuildContext.ModIconPath, $"{modDir}/modicon.png");
			}

			context.Zip(modDir, $"{modDir}_{context.Version}.zip");
			context.DeleteDirectory(modDir, new() { Recursive = true });
		}
	}

    [TaskName("Publish")]
    [IsDependentOn(typeof(PackageTask))]
    public class PublishTask : FrostingTask<BuildContext>
    {
        public override void Run(BuildContext context)
        {
			//TODO: gh publish create
        }
    }

    [TaskName("Default")]
	[IsDependentOn(typeof(PublishTask))]
	public class DefaultTask : FrostingTask
	{
	}
}