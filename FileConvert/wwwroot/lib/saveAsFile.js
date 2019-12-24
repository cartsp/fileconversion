function saveAsFile(filename, bytesBase64) {
    if (window.navigator && window.navigator.msSaveOrOpenBlob) { // Needed for Edge
        var byteCharacters = atob(bytesBase64);
        var byteNumbers = new Array(byteCharacters.length);
        for (var i = 0; i < byteCharacters.length; i++) {
            byteNumbers[i] = byteCharacters.charCodeAt(i);
        }
        var byteArray = new Uint8Array(byteNumbers);
        var newBlob = new Blob([byteArray], { type: "application/octet-stream" })
        window.navigator.msSaveOrOpenBlob(newBlob, filename); 
    } else {
        var link = document.createElement('a');
        link.download = filename;
        link.href = "data:application/octet-stream;base64," + bytesBase64;
        document.body.appendChild(link); // Needed for Firefox
        link.click();
        document.body.removeChild(link);
    }
}
