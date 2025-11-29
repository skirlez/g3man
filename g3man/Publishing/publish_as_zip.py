import os
import shutil
import zipfile
import subprocess


if os.name == "posix":
    runtime = "linux-x64"
    zip_suffix = "linux"
elif os.name == "nt":
    runtime = "win-x64"
    zip_suffix = "windows"


# I really don't know what these are for. But we don't support other languages right now
locales = ["en"]

if os.path.isdir("./package"):
    print("Deleting previous package folder...")
    shutil.rmtree("./package")


status = subprocess.run(
    ["dotnet",  "publish", "-c", "Release", "-o", "Publishing/package/bin", "--runtime", runtime],
    cwd = ".."
)
if status.returncode != 0:
    exit()
    
print("Copying to zip...")

def copy_all_to_zip(f, dir):
    for root, dirs, files in os.walk(dir):
        for file in files:
            full_path = os.path.join(root, file)
            relative_path = os.path.relpath(full_path, dir)
            f.write(full_path, relative_path)


with zipfile.ZipFile(f"./g3man-{zip_suffix}.zip", 'w', zipfile.ZIP_DEFLATED) as f:
    copy_all_to_zip(f, "./package")
    if os.name == "nt":
        f.writestr("readme.txt", "The g3man executable (g3man.exe) is in the bin folder.\n" \
                               + "If you are extracting this ZIP, don't leave out the share folder. It is required.")


