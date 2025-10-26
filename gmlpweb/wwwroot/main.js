async function onBlazorInitialized() {
  patchDisplay.textContent = patchEditor.value;
  codeDisplay.textContent = codeEditor.value;
  applyPatch();
}

const patchOption = document.getElementById("patch");
const decompileOption = document.getElementById("decompile");
const disassembleOption = document.getElementById("disassemble");

const patchEditor = document.getElementById("patchEditor");
const patchDisplay = document.getElementById("patchDisplay");
const codeEditor = document.getElementById("codeEditor");
const codeDisplay = document.getElementById("codeDisplay");
const patchedCode = document.getElementById("patchedCode");
const terminal = document.getElementById("terminal");

function refreshHighlights() {
  codeDisplay.removeAttribute("data-highlighted");
  patchDisplay.removeAttribute("data-highlighted");
  patchedCode.removeAttribute("data-highlighted");
  hljs.highlightAll();
}

patchEditor.addEventListener("input", (event) => {
  patchDisplay.textContent = patchEditor.value;
  applyPatch();
});

codeEditor.addEventListener("input", (event) => {
  codeDisplay.textContent = codeEditor.value;
  applyPatch();
});

patchOption.onclick = () => {
  applyPatch();
};

decompileOption.onclick = () => {
  applyPatch();
};

disassembleOption.onclick = () => {
  applyPatch();
};

async function applyPatch() {
  let patched = await DotNet.invokeMethodAsync(
    "gmlpweb",
    "patch",
    patchEditor.value,
    codeEditor.value,
  );

  if (patched.type === 1) {
    // error
    terminal.classList.add("error");
    terminal.textContent = patched.result;
    return;
  } else if (patched.type === 2) {
    // exception
    terminal.classList.add("error");
    terminal.textContent = patched.result;
    return;
  }

  if (disassembleOption.checked) {
    patched.result = await DotNet.invokeMethodAsync(
      "gmlpweb",
      "compile_and_disassemble",
      patched.result,
    );
  } else if (decompileOption.checked) {
    patched.result = await DotNet.invokeMethodAsync(
      "gmlpweb",
      "compile_and_decompile",
      patched.result,
    );
  }

  patchedCode.textContent = patched.result;
  terminal.textContent = "All quiet on the western front.";
  terminal.classList.remove("error");

  refreshHighlights();
}
