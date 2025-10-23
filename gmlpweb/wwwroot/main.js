
async function onBlazorInitialized() {
    const result = await DotNet.invokeMethodAsync('gmlpweb', 'patch', 
        'meta:\n' +
        'target=test\n' +
        'critical=false\n' +
        'patch:\n' +
        'find_line_with(\'aaaaabbbbb\')\n' +
        'write_after(\'1\')',

        'ccccc;\n' +
        'aaaaabbbbb;\n' +
        'ddddd;');
    console.log(result);
}