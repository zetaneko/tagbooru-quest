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

window.positionDropdown = (dropdownId) => {
    const dropdown = document.getElementById(dropdownId);
    if (!dropdown) {
        console.log('Dropdown not found:', dropdownId);
        return;
    }

    // Find the parent tag tile by looking for the tile that contains the dropdown
    const tagContainer = dropdown.closest('.tag-item-container');
    if (!tagContainer) {
        console.log('Tag container not found for:', dropdownId);
        return;
    }

    const tagTile = tagContainer.querySelector('.tag-tile');
    if (!tagTile) {
        console.log('Tag tile not found for:', dropdownId);
        return;
    }

    // Move dropdown to document body to escape all container clipping
    if (dropdown.parentNode !== document.body) {
        console.log('Moving dropdown to document body');
        document.body.appendChild(dropdown);
    }

    // Get the position of the parent tag tile
    const tileRect = tagTile.getBoundingClientRect();

    // Add scroll offsets to get absolute position
    const scrollLeft = window.pageXOffset || document.documentElement.scrollLeft;
    const scrollTop = window.pageYOffset || document.documentElement.scrollTop;

    // Position off-screen first to measure dimensions
    dropdown.style.visibility = 'hidden';
    dropdown.style.opacity = '1';
    dropdown.style.top = '0px';
    dropdown.style.left = '0px';

    const dropdownRect = dropdown.getBoundingClientRect();

    console.log('Tag tile rect:', tileRect);
    console.log('Dropdown rect:', dropdownRect);
    console.log('Scroll offsets:', scrollLeft, scrollTop);

    // Calculate position - center horizontally, position below the tag with proper gap
    // Use absolute positioning relative to the page (not viewport)
    let left = tileRect.left + scrollLeft + (tileRect.width / 2) - (dropdownRect.width / 2);
    let top = tileRect.bottom + scrollTop + 15; // 15px gap below the tile for the arrow

    // Keep dropdown within viewport
    const margin = 20;
    const viewportWidth = window.innerWidth;
    const viewportHeight = window.innerHeight;

    // Adjust horizontal position (relative to viewport)
    const leftRelativeToViewport = left - scrollLeft;
    if (leftRelativeToViewport < margin) {
        left = scrollLeft + margin;
    } else if (leftRelativeToViewport + dropdownRect.width > viewportWidth - margin) {
        left = scrollLeft + viewportWidth - dropdownRect.width - margin;
    }

    // Adjust vertical position if dropdown goes off bottom (relative to viewport)
    const topRelativeToViewport = top - scrollTop;
    if (topRelativeToViewport + dropdownRect.height > viewportHeight - margin) {
        top = tileRect.top + scrollTop - dropdownRect.height - 15; // Show above instead with gap
    }

    // Apply final position and make visible
    dropdown.style.left = left + 'px';
    dropdown.style.top = top + 'px';
    dropdown.style.visibility = 'visible';
    dropdown.style.opacity = '';

    console.log('Final positioned dropdown:', dropdownId, 'at', left, top);
    console.log('Tile absolute position:', tileRect.left + scrollLeft, tileRect.bottom + scrollTop);

    // Set up click outside to close (only if not already set up)
    if (!dropdown.hasAttribute('data-click-outside-setup')) {
        dropdown.setAttribute('data-click-outside-setup', 'true');
        const handleClickOutside = (event) => {
            if (!dropdown.contains(event.target) && !tagTile.contains(event.target)) {
                dropdown.classList.remove('show');
                dropdown.classList.add('hide');

                // Notify Blazor about the state change
                const dropdownIdParts = dropdownId.split('dropdown-')[1];
                if (dropdownIdParts && window.blazorDropdownClosed) {
                    window.blazorDropdownClosed(dropdownIdParts);
                }

                document.removeEventListener('click', handleClickOutside);
                dropdown.removeAttribute('data-click-outside-setup');
            }
        };
        // Delay to prevent immediate triggering
        setTimeout(() => {
            document.addEventListener('click', handleClickOutside);
        }, 100);
    }
};

window.hideDropdown = (dropdownId) => {
    const dropdown = document.getElementById(dropdownId);
    if (dropdown) {
        dropdown.classList.remove('show');
        dropdown.classList.add('hide');
        console.log('Hidden dropdown:', dropdownId);
    }
};

window.positionBadges = () => {
    const badges = document.querySelectorAll('.selection-badge');
    badges.forEach(badge => {
        const tabButton = badge.closest('.category-tab');
        if (tabButton) {
            const rect = tabButton.getBoundingClientRect();
            const scrollLeft = window.pageXOffset || document.documentElement.scrollLeft;
            const scrollTop = window.pageYOffset || document.documentElement.scrollTop;

            // Position badge at top-right of tab
            badge.style.left = (rect.right + scrollLeft - 13) + 'px'; // 13px = half badge width
            badge.style.top = (rect.top + scrollTop - 13) + 'px'; // 13px = half badge height
        }
    });
};

// Position badges when page loads and on resize/scroll
window.addEventListener('load', window.positionBadges);
window.addEventListener('resize', window.positionBadges);
window.addEventListener('scroll', window.positionBadges);

window.setupDropdownCallbacks = (dotNetRef) => {
    window.blazorDropdownClosed = (childrenKey) => {
        dotNetRef.invokeMethodAsync('OnDropdownClosed', childrenKey);
    };
    window.characterDesignerRef = dotNetRef;
};