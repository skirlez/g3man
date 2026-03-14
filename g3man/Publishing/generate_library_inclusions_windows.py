import os

magic_string = "REPLACE_HERE"

bundle_gtk_targets_template = rf"""
<Project xmlns="http://schemas.microsoft.com/developer/msbuild/2003">

  <!-- Thanks to the Pinta project for figuring this out https://github.com/PintaProject/Pinta -->
  <!-- Install GTK library dependencies on Windows, from the MSYS installation. -->
  <PropertyGroup>
    <!-- Note this can be overridden by an environment variable with the same name. -->
    <MinGWFolder>C:\msys64\mingw64</MinGWFolder>
    <MinGWBinFolder>$(MinGWFolder)\bin</MinGWBinFolder>
  </PropertyGroup>

  <ItemGroup>
    {magic_string}

    <GtkFile Include="$(MinGWBinFolder)\gdbus.exe" />
    <GtkFile Include="$(MinGWBinFolder)\gspawn-win64-helper.exe" />
    <GtkFile Include="$(MinGWBinFolder)\gspawn-win64-helper-console.exe" />
    <GtkFile Include="$(MinGWBinFolder)\gtk4-query-settings.exe" />
    <GtkFile Include="$(MinGWBinFolder)\gtk4-update-icon-cache.exe" />

    <GtkFile Include="Publishing\default-glib-schemas\*" Link="default-glib-schemas\%(Filename)%(Extension)"/>
  </ItemGroup>

  <ItemGroup>
    <Content Include="@(GtkFile)">
      <CopyToOutputDirectory>PreserveNewest</CopyToOutputDirectory>
    </Content>
  </ItemGroup>
</Project>
"""

if __name__ == "__main__":
    if not os.path.isfile("libraries.txt"):
        print("Please make a libraries.txt with the required libraries.")
        print("Should be the output of `ldd libadwaita-1-0.dll | grep \"\\/mingw.*\\.dll\" -o`")
        exit()
    with open("libraries.txt", "rt") as f:
        includes = "<GtkFile Include=\"$(MinGWBinFolder)\\libadwaita-1-0.dll\" />"
        for line in f:
            name = os.path.basename(line.strip())
            includes += f"\n    <GtkFile Include=\"$(MinGWBinFolder)\\{name}\" />"
    with open("bundle_gtk.targets", "wt") as f:
        f.write(bundle_gtk_targets_template.replace(magic_string, includes))
    print("Updated bundle_gtk.targets!")
