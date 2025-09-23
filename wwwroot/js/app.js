// JavaScript helper functions for TagbooruQuest

window.getClickedElement = (event) => {
    console.log('Click detected:', event);
    const target = event.target;
    console.log('Target element:', target);
    console.log('Target attributes:', target.attributes);

    if (target && target.hasAttribute('data-bodypart')) {
        const bodyPart = target.getAttribute('data-bodypart');
        console.log('Body part found:', bodyPart);
        return bodyPart;
    }

    console.log('No body part data found');
    return null;
};

window.copyToClipboard = async (text) => {
    try {
        await navigator.clipboard.writeText(text);
        return true;
    } catch (err) {
        console.error('Failed to copy text: ', err);
        return false;
    }
};

window.downloadFile = (filename, content, contentType = 'text/plain') => {
    const blob = new Blob([content], { type: contentType });
    const url = window.URL.createObjectURL(blob);
    const link = document.createElement('a');
    link.href = url;
    link.download = filename;
    document.body.appendChild(link);
    link.click();
    document.body.removeChild(link);
    window.URL.revokeObjectURL(url);
};