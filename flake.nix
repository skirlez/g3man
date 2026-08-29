{
  description = "";

  inputs.nixpkgs.url = "github:NixOS/nixpkgs/nixos-unstable";
  inputs.self.submodules = true;
  outputs =
    { self, nixpkgs, ... }:
    let
      system = "x86_64-linux";
      pkgs = import nixpkgs { inherit system; };

      libg3man = pkgs.stdenv.mkDerivation {
        src = ./c;
        name = "libg3man";
        nativeBuildInputs = with pkgs; [
          cmake
          patchelf
        ];
        installPhase = ''
        	runHook preInstall
          mkdir -p $out/lib
          cp libg3man.so $out/lib
          cp libxdelta3.so $out/lib
          cp libgit2.so $out/lib
          runHook postInstall
        '';
        postInstall = ''
          patchelf --set-rpath "$out/lib" $out/lib/libg3man.so       
        '';
        strictDeps = true;
        __structuredAttrs = true;
      };

      g3man = pkgs.buildDotnetModule {
        pname = "g3man";
        version = "10.1.0";
        src = builtins.filterSource (
          path: type: type != "directory" || (baseNameOf path != "gmlpweb" && baseNameOf path != ".github")
        ) ./.;

        projectFile = "g3man/g3man.csproj";

        # generated via:
        # dotnet restore --packages=packageDir g3man && dotnet restore --packages=packageDir gmlpv2.Tests && nix run nixpkgs#nuget-to-json -- packageDir > deps-nix.json && rm -r packageDir
        # (from https://wiki.nixos.org/wiki/DotNET)
        nugetDeps = ./deps-nix.json;

        projectReferences = [ ];

        nativeBuildInputs = with pkgs; [
          wrapGAppsHook4
          # UndertaleModLib uses git to generate a hash or something but it fails silently so it's not REALLY needed
        ];

        runtimeDeps = with pkgs; [
          gtk4
          libadwaita
          libg3man
        ];

        doCheck = true;
        testProjectFile = [
          "gmlpv2.Tests"
        ];

        dotnetFlags = [
          # We build the c libraries the Nix way
          "/p:DontHandleCLibs=true"
          # consistently getting a "used by another process" error unless i set this (weird)
          "-m:1"
        ];

        strictDeps = true;
        dotnet-sdk = pkgs.dotnetCorePackages.sdk_10_0;
        dotnet-runtime = pkgs.dotnetCorePackages.runtime_10_0;
        executables = [ "g3man" ];
      };

      devshell = pkgs.mkShell {
        packages = with pkgs; [
          dotnetCorePackages.sdk_10_0
          glib # for GSETTINGS_SCHEMAS_PATH

          llvmPackages.clang-tools

          cmake
          bear
        ];
        strictDeps = true;
        shellHook = ''
          export LD_LIBRARY_PATH=${
            pkgs.lib.makeLibraryPath [
              pkgs.gtk4
              pkgs.libadwaita
            ]
          }
          export XDG_DATA_DIRS=$XDG_DATA_DIRS:$GSETTINGS_SCHEMAS_PATH
          export DOTNET_ROOT=${pkgs.dotnetCorePackages.sdk_10_0}/share/dotnet
          # don't actually need this?
          # export XDG_DATA_DIRS=$XDG_DATA_DIRS:${pkgs.hicolor-icon-theme}/share:${pkgs.adwaita-icon-theme}/share
        '';
      };

      # the automated fetch-deps script is Kinda Weird, and doesn't seem
      # to account for the libraries needed to run the tests, so this is how the nuget lockfile
      # is made
      update-nuget-lockfile = pkgs.writeShellApplication {
        name = "update-nuget-lockfile";
        runtimeInputs = with pkgs; [
          pkgs.dotnetCorePackages.sdk_10_0
          nuget-to-json
        ];
        text = ''
          TMPDIR=$(mktemp -d)
          echo Restoring packages and putting them in "$TMPDIR"
          dotnet restore --packages="$TMPDIR" g3man
          dotnet restore --packages="$TMPDIR" gmlpv2.Tests 
          nuget-to-json "$TMPDIR" > deps-nix.json'';
      };

    in
    {
      devShells.x86_64-linux.default = devshell;
      packages.x86_64-linux = {
        default = g3man;
        inherit libg3man;
        inherit update-nuget-lockfile;
      };
      formatter.x86_64-linux = nixpkgs.legacyPackages.${system}.nixfmt-tree;
    };
}
