import os

if __name__ == "__main__":

	if not os.path.isfile("libraries.txt"):
		print("Please make a libraries.txt with the required libraries.")
		print("Should be the output of `ldd libadwaita-1-0.dll | grep '\\/mingw.*\\.dll' -o`")
	with open("libraries.txt") as f:
		print("<GtkFile Include=\"$(MinGWBinFolder)\\" + "libadwaita-1-0.dll" + "\" />")
		for line in f:
			name = os.path.basename(line.strip())
			print(
				"<GtkFile Include=\"$(MinGWBinFolder)\\" + name + "\" />"
			)
