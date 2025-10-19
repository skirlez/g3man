{
  description = "";

  inputs.nixpkgs.url = "github:NixOS/nixpkgs";

  outputs = { self, nixpkgs, ... }:
    let
      system = "x86_64-linux";
      pkgs = import nixpkgs { inherit system; };
    in {
      devShells.x86_64-linux.default = pkgs.mkShell {
        packages = with pkgs; [
          dotnetCorePackages.sdk_8_0
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
          export DOTNET_ROOT=${pkgs.dotnetCorePackages.sdk_8_0}
          # don't actually need this?
          # export XDG_DATA_DIRS=$XDG_DATA_DIRS:${pkgs.hicolor-icon-theme}/share:${pkgs.adwaita-icon-theme}/share
        '';
      };
    };
}
