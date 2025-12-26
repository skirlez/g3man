{
  description = "";

  inputs.nixpkgs.url = "github:NixOS/nixpkgs/nixos-unstable";

  outputs = { self, nixpkgs, ... }:
    let
      system = "x86_64-linux";
      pkgs = import nixpkgs { inherit system; };

      g3man = pkgs.buildDotnetModule {
        pname = "g3man";
        version = "4";
        src = builtins.filterSource
          (path: type: type != "directory" || (baseNameOf path != "gmlpweb" && baseNameOf path != ".github"))
          ./.;

        projectFile = "g3man";

        # generated via
        # dotnet restore --packages=packageDir ./g3man/g3man.csproj
        # nuget-to-json packageDir > g3man-deps.json
        # rm -r packageDir
        # (from https://wiki.nixos.org/wiki/DotNET)
        nugetDeps = ./g3man-deps.json;

        projectReferences = [ ];

        nativeBuildInputs = with pkgs; [
          # UndertaleModLib uses git to generate a hash or something but it fails silently so it's not REALLY needed
          wrapGAppsHook4
        ];

        runtimeDeps = with pkgs; [
          gtk4
          libadwaita
        ];


        dotnet-sdk = pkgs.dotnetCorePackages.sdk_8_0;
        dotnet-runtime = pkgs.dotnetCorePackages.runtime_8_0;
        executables = [ "g3man" ];
      };


    in {
      devShells.x86_64-linux.default = pkgs.mkShell {
        packages = with pkgs; [
          dotnetCorePackages.sdk_8_0
          glib # for GSETTINGS_SCHEMAS_PATH
          git # UndertaleModLib uses it
        ];
        buildInputs = with pkgs; [
          git
          gtk4
          libadwaita
        ];
        strictDeps = true;
        shellHook = ''
          export LD_LIBRARY_PATH=${pkgs.lib.makeLibraryPath 
                      [ pkgs.gtk4 pkgs.libadwaita ]}
          export XDG_DATA_DIRS=$XDG_DATA_DIRS:$GSETTINGS_SCHEMAS_PATH
          export DOTNET_ROOT=${pkgs.dotnetCorePackages.sdk_8_0}/share/dotnet
          # don't actually need this?
          # export XDG_DATA_DIRS=$XDG_DATA_DIRS:${pkgs.hicolor-icon-theme}/share:${pkgs.adwaita-icon-theme}/share
        '';
      };
      packages.x86_64-linux.default = g3man;
    };
}
