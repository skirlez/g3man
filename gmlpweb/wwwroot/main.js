async function onBlazorInitialized() {}

const code = document.getElementById("code");
const patch = document.getElementById("patch");
const result = document.getElementById("result");
const terminal = document.getElementById("terminal");

async function applyPatch() {
  try {
    const patched = await DotNet.invokeMethodAsync(
      "gmlpweb",
      "patch",
      patch.value,
      code.value,
    );
    result.innerText = patched;
    terminal.innerText = "";
  } catch (error) {
    terminal.innerText = error.message;
  }
}

code.addEventListener("input", (event) => {
  applyPatch();
});
patch.addEventListener("input", (event) => {
  applyPatch();
});
