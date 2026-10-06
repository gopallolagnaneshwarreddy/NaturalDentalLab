// Saves a file sent from the server (Blazor DotNetStreamReference) straight to
// the user's Downloads folder, without opening the print dialog.
window.downloadFileFromStream = async function (fileName, contentStreamReference, contentType) {
    const buffer = await contentStreamReference.arrayBuffer();
    const blob = new Blob([buffer], { type: contentType || 'application/pdf' });
    const url = URL.createObjectURL(blob);
    const link = document.createElement('a');
    link.href = url;
    link.download = fileName || 'download.pdf';
    document.body.appendChild(link);
    link.click();
    link.remove();
    setTimeout(function () { URL.revokeObjectURL(url); }, 1500);
};
