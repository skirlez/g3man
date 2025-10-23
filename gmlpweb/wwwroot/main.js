
setTimeout(async () => {
    const result = await DotNet.invokeMethodAsync('gmlpweb', 'concat', 'aaa', 'bbb');
    console.log(result);
}, 1000);