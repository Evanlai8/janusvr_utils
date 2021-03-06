using UnrealBuildTool;

public class JanusExporterModule : ModuleRules
{
    public JanusExporterModule(TargetInfo Target)
	{
        PrivateIncludePaths.AddRange(
            new string[]
            {
                @"C:\Dev\UnrealEngine\Engine\Source\ThirdParty\FBX\2016.1.1\include",
            }
        );

        PublicAdditionalLibraries.Add(@"C:\Dev\UnrealEngine\Engine\Source\ThirdParty\FBX\2016.1.1\lib\vs2015\x64\release\libfbxsdk.lib");

        PublicDependencyModuleNames.AddRange(
			new string[] {
				"Core",
				"CoreUObject",
				"Engine",
				"Slate",
				"UnrealEd",
                "UElibPNG",
                "MaterialUtilities",
                "RenderCore",
                "RHI"
            }
		);
		
		PrivateDependencyModuleNames.AddRange(
			new string[] {
                "Engine",
                "InputCore",
				"SlateCore",
				"PropertyEditor",
				"LevelEditor",
                "MaterialUtilities",
                "RenderCore",
                "RHI"
            }
        );

        AddThirdPartyPrivateStaticDependencies(Target, "FBX");
    }
}