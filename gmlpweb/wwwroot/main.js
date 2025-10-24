async function onBlazorInitialized() {
  hljs.highlightAll();
}

const exampleButton = document.getElementById("example");
const codeEditor = document.getElementById("codeEditor");
const codeDisplay = document.getElementById("codeDisplay");
const patchEditor = document.getElementById("patchEditor");
const patchDisplay = document.getElementById("patchDisplay");
const result = document.getElementById("result");
const terminal = document.getElementById("terminal");

function refreshHighlights() {
  codeDisplay.removeAttribute("data-highlighted");
  patchDisplay.removeAttribute("data-highlighted");
  result.removeAttribute("data-highlighted");
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

async function applyPatch() {
  const patched = await DotNet.invokeMethodAsync(
    "gmlpweb",
    "patch",
    patchEditor.value,
    codeEditor.value);
    
  if (patched.type === 0) {
    result.textContent = patched.result;
    terminal.textContent = "All quiet on the western front.";
    terminal.classList.remove("error");
  }
  else if (patched.type === 1) {
    terminal.classList.add("error");
    terminal.textContent = patched.result;
  }
  else {
    console.log(patched.result);
    terminal.classList.add("error");
    terminal.textContent = patched.result;
  }
  refreshHighlights();
}

exampleButton.addEventListener("click", (event) => {
  patchEditor.value = `meta:
target=test
critical=false
patch:
find_line_with('Up key')
write_after('    show_message("Patch applied!");')`;
  codeEditor.value = `if (keyboard_check_pressed(vk_up))
{
    show_message("Up key hit!");
}
else if (keyboard_check_pressed(vk_down))
{
    show_message("Down key hit!");
}`;
  result.textContent = `if (keyboard_check_pressed(vk_up))
{
    show_message("Up key hit!");
    show_message("Patch applied!");
}
else if (keyboard_check_pressed(vk_down))
{
    show_message("Down key hit!");
}`;

  patchDisplay.textContent = patchEditor.value;
  patchDisplay.removeAttribute("data-highlighted");

  codeDisplay.textContent = codeEditor.value;
  codeDisplay.removeAttribute("data-highlighted");

  result.removeAttribute("data-highlighted");

  hljs.highlightAll();
});
