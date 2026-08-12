#!/usr/bin/env python3

import os
import sys
import shutil
import zipfile
import subprocess


if os.name == "posix":
	runtime = "linux-x64"
	zip_suffix = "linux-amd64"
	extra_args = []
elif os.name == "nt":
	runtime = "win-x64"
	zip_suffix = "windows-amd64"
else:
	print("Unsupported environment")
	exit()


if len(sys.argv) < 1 or len(sys.argv) > 2:
	name = "publish.py" if len(sys.argv) == 0 else sys.argv[0]
	print(f"Usage: {name} [--zip]")
	exit()



if os.path.isdir("./package"):
	print("Deleting previous package folder...")
	shutil.rmtree("./package")
status = subprocess.run(
	["dotnet", "publish", "g3man.csproj", "-c", "Release", "-o", "Publishing/package/g3man", "--runtime", runtime, "-m:1"],
	cwd = os.path.abspath("..")
)
if status.returncode != 0:
	exit(status.returncode)

if os.name == "nt":
	mingwroot = os.environ.get("MINGWROOT", "C:\\msys64\\mingw64")
	result = subprocess.run(
		[f"{mingwroot}/../usr/bin/ldd", f"{mingwroot}/bin/libadwaita-1-0.dll"],
		capture_output = True,
		text = True)
	if (result.returncode != 0):
		exit()
	dependencies = result.stdout.split("\n")
	
	shutil.copy(f"{mingwroot}/bin/libadwaita-1-0.dll", "./package/g3man")
	count = 1
	for line in set(dependencies):
		if "/mingw64/bin/" not in line:
			continue
		count += 1
		start = line.find("=>") + 3
		end = line.find(" ", start)
		dll = line[start:end].replace("/mingw64/bin/", f"{mingwroot}/bin/").replace("\\", "/")
		print(dll)
		shutil.copy(dll, "./package/g3man")
	print(f"Copied {count} GTK4 dependencies")
	
	shutil.copytree("./default-glib-schemas", "./package/g3man/default-glib-schemas")

print("All done!")

def copy_all_to_zip(f, dir):
	for root, dirs, files in os.walk(dir):
		for file in files:
			full_path = os.path.join(root, file)
			relative_path = os.path.relpath(full_path, dir)
			f.write(full_path, relative_path)



if "--zip" in sys.argv:
	print("Copying to zip...")
	with zipfile.ZipFile(f"./g3man-{zip_suffix}.zip", 'w', zipfile.ZIP_DEFLATED, strict_timestamps=False) as f:
	 	 copy_all_to_zip(f, "./package")
