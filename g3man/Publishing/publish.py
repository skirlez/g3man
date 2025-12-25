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
	mingwroot = os.environ.get("MINGWROOT", "C:\\msys64\\mingw64")
	extra_args = [f"/p:MinGWFolder=\"{mingwroot}\""]


if len(sys.argv) < 1 or len(sys.argv) > 2:
	name = "publish.py" if len(sys.argv) == 0 else sys.argv[0]
	print(f"Usage: {name} [--zip]")
	exit()



if os.path.isdir("./package"):
	print("Deleting previous package folder...")
	shutil.rmtree("./package")
status = subprocess.run(
	["dotnet", "publish", "g3man.csproj", "-c", "Release", "-o", "Publishing/package/bin", "--runtime", runtime] + extra_args,
	cwd = os.path.abspath("..")
)
if status.returncode != 0:
	exit(status.returncode)


def copy_all_to_zip(f, dir):
	for root, dirs, files in os.walk(dir):
		for file in files:
			full_path = os.path.join(root, file)
			relative_path = os.path.relpath(full_path, dir)
			f.write(full_path, relative_path)



if "--zip" in sys.argv:
	print("Copying to zip...")
	with zipfile.ZipFile(f"./g3man-{zip_suffix}.zip", 'w', zipfile.ZIP_DEFLATED) as f:
	 	 copy_all_to_zip(f, "./package/bin")



