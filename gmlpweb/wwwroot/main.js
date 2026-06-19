

const pDiv = document.getElementById("patchEditor");
const cDiv = document.getElementById("codeEditor");
const pcDiv = document.getElementById("patchedCodeEditor");

const patchOption = document.getElementById("patch");
const decompileOption = document.getElementById("decompile");
const disassembleOption = document.getElementById("disassemble");

const terminal = document.getElementById("terminal");

async function onBlazorInitialized() {
  pDiv.textContent = 
`
local g3man = require "g3man"

g3man.patch("code entry name(s) would go here", function(t)
  -- adding code after a condition
  local i
  i = t:find_line_with(1, 'if (condition)')
  t:write(i + 1, 'show_message("Patch applied!")')

  -- adding more conditions to an if statement
  t:write_before(1, 'added_condition = true')
  
  i = t:find_line_with(1, 'if (condition)')
  t:write(i, '&& added_condition')

  -- removing unwanted code
  i = t:find_line_with(1, 'I hope')
  t:write_before(i, 'if false {')
  t:write(i, '}')
end)


-- https://github.com/skirlez/g3man/wiki/Introduction-to-gmlp`
  pEditor = ace.edit(pDiv, {
    theme : "ace/theme/one_dark",
    showPrintMargin : false,
    mode : "ace/mode/lua",
    useWorker : false
  });
  pEditor.session.on("change", function() {
    applyPatch();
  })

  cDiv.textContent = 
`condition = true;

if (condition)
{
    show_message("Condition met!");
}
else
{
    show_message("Condition not met...");
    show_message("I hope no one patches this out");
}`
  cEditor = ace.edit(cDiv, {
    theme : "ace/theme/one_dark",
    showPrintMargin : false,
    mode : "ace/mode/javascript",
    useWorker : false
  });
  cEditor.session.on("change", function() {
    applyPatch();
  })

  pcDiv.style.visiblity = "visible";
  pcEditor = ace.edit(pcDiv, {
    theme : "ace/theme/one_dark",
    showPrintMargin : false,
    mode : "ace/mode/javascript",
    readOnly : true,
    useWorker : false
  });

  
  applyPatch();
}


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
  DotNet.invokeMethodAsync(
    "gmlpweb",
    "patch",
    pEditor.getValue(),
    cEditor.getValue(),
  ).then(async patched => {
    
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

    let output;
    if (patchOption.checked) {
      output = patched;
    } else if (disassembleOption.checked) {
      output = await DotNet.invokeMethodAsync(
          "gmlpweb",
          "compile_and_disassemble",
          patched.result,
      );
    } else if (decompileOption.checked) {
      output = await DotNet.invokeMethodAsync(
          "gmlpweb",
          "compile_and_decompile",
          patched.result,
      );
    }
    if (output.type === 1) {
      // compilation fail
      terminal.classList.add("error");
      terminal.textContent = output.result;
      output = patched;
    } else {
      terminal.textContent = "All quiet on the western front.";
      terminal.classList.remove("error");
    }

    pcEditor.setValue(output.result);
    pcEditor.clearSelection();
  });
}
