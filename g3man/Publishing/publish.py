import os
import sys
import shutil
import zipfile
import subprocess


if os.name == "posix":
	runtime = "linux-x64"
	zip_suffix = "linux-amd64"
	output_folder = "./package/bin"
	extra_args = []
elif os.name == "nt":
	runtime = "win-x64"
	zip_suffix = "windows-amd64"
	mingwroot = os.environ.get("MINGWROOT", "C:\\msys64\\mingw64")
	output_folder = "./package"
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

if os.name == "nt":
	with open(f"{output_folder}/readme.txt", 'w') as f:
		f.write("The g3man executable (g3man.exe) is in the bin folder.\n"
				+ "If you are extracting this ZIP, don't leave out the share folder. It is required.")
		# I really don't know what these are for. But we don't support other languages right now
		# TODO: actually make this delete the other locale folders (i forgot)
		for dir in os.listdir(f"{output_folder}/share/locale"):
			if not dir.startswith('en'):
				shutil.rmtree(f"{output_folder}/share/locale/{dir}")

def copy_all_to_zip(f, dir):
	for root, dirs, files in os.walk(dir):
		for file in files:
			full_path = os.path.join(root, file)
			relative_path = os.path.relpath(full_path, dir)
			f.write(full_path, relative_path)



if len(sys.argv) == 2 and sys.argv[1] == "--zip":
	print("Copying to zip...")
	with zipfile.ZipFile(f"./g3man-{zip_suffix}.zip", 'w', zipfile.ZIP_DEFLATED) as f:
	 	 copy_all_to_zip(f, output_folder)



