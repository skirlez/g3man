WebAssembly.instantiateStreaming(fetch("gmlpweb.wasm"), importObject).then(
    (obj) => {
        // Call an exported function:
        console.log(obj.instance.exports.concat_test("test1", "test2"));
    },
);
