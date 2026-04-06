{
  description = "";

  inputs.nixpkgs.url = "github:NixOS/nixpkgs/nixos-unstable";

  outputs = { self, nixpkgs, ... }:
  let
    system = "x86_64-linux";
    pkgs = import nixpkgs { inherit system; };
  
    mkG3manLib = { pname, version, buildInputs ? [], soname, flags }: pkgs.stdenv.mkDerivation {
      inherit pname version buildInputs;
      src = ./c;
      nativeBuildInputs = with pkgs; [
        cmake
      ];
      installPhase = ''
        mkdir -p $out/lib
        cp ${soname} $out/lib
      '';
      cmakeFlags = flags;
    };
    
    libxdelta = mkG3manLib {
      pname = "libxdelta";
      version = "3.1.0";
      soname = "libxdelta3.so";
      flags = [ "-DG3MAN_SKIP_LIBG3MAN=ON" ];
    };
    
    libg3man = mkG3manLib {
      pname = "libg3man";
      version = "1.0.0";
      buildInputs = [ libxdelta ];
      soname = "libg3man.so";
      flags = [ "-DG3MAN_SKIP_LIBXDELTA=ON" ];
    };
    
    g3man = pkgs.buildDotnetModule {
      pname = "g3man";
      version = "7";
      src = builtins.filterSource
      (path: type: type != "directory" || (baseNameOf path != "gmlpweb" && baseNameOf path != ".github"))
      ./.;
  
      projectFile = "g3man";
  
      # generated via
      # dotnet restore --packages=packageDir ./g3man/g3man.csproj && nuget-to-json packageDir > g3man-deps.json && rm -r packageDir
      # (from https://wiki.nixos.org/wiki/DotNET)
      nugetDeps = ./g3man-deps.json;
  
      projectReferences = [];
  
      nativeBuildInputs = with pkgs; [
        wrapGAppsHook4
        # UndertaleModLib uses git to generate a hash or something but it fails silently so it's not REALLY needed
      ];
  
      runtimeDeps = with pkgs; [
        gtk4
        libadwaita
        libxdelta
        libg3man
      ];
  
      # We build the c libraries the Nix way
      dotnetFlags = [ "/p:DontHandleCLibs=true" ];
  
      dotnet-sdk = pkgs.dotnetCorePackages.sdk_10_0;
      dotnet-runtime = pkgs.dotnetCorePackages.runtime_10_0;
      executables = ["g3man"];
    };


  in {
    devShells.x86_64-linux.default = pkgs.mkShell {
      packages = with pkgs; [
        dotnetCorePackages.sdk_10_0
        glib # for GSETTINGS_SCHEMAS_PATH
        
        llvmPackages.clang-tools
        cmake
        bear
      ];
      strictDeps = true;
      shellHook = ''
        export LD_LIBRARY_PATH=${pkgs.lib.makeLibraryPath 
        [ pkgs.gtk4 pkgs.libadwaita ]}
        export XDG_DATA_DIRS=$XDG_DATA_DIRS:$GSETTINGS_SCHEMAS_PATH
        export DOTNET_ROOT=${pkgs.dotnetCorePackages.sdk_10_0}/share/dotnet
        # don't actually need this?
        # export XDG_DATA_DIRS=$XDG_DATA_DIRS:${pkgs.hicolor-icon-theme}/share:${pkgs.adwaita-icon-theme}/share
      '';
    };
    packages.x86_64-linux.default = g3man;
  };
}
