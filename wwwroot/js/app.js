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

    // Clear any previous hiding state and reset all properties
    dropdown.classList.remove('hide');
    dropdown.classList.add('show');
    dropdown.style.visibility = '';  // Reset to default
    dropdown.style.opacity = '';      // Reset to default

    // Find or retrieve the parent tag tile reference
    let tagContainer, tagTile;

    // If dropdown is already in document.body, use stored references
    if (dropdown.parentNode === document.body && dropdown.dataset.originalTagId) {
        tagTile = document.getElementById(dropdown.dataset.originalTagId);
        if (tagTile) {
            tagContainer = tagTile.closest('.tag-item-container');
        }
    } else {
        // Find the parent tag tile by looking for the tile that contains the dropdown
        tagContainer = dropdown.closest('.tag-item-container');
        if (tagContainer) {
            tagTile = tagContainer.querySelector('.tag-tile');
        }
    }

    if (!tagContainer || !tagTile) {
        console.log('Tag container or tile not found for:', dropdownId);
        return;
    }

    // Move dropdown to document body to escape all container clipping
    if (dropdown.parentNode !== document.body) {
        // Store reference to original tag tile before moving
        if (tagTile.id) {
            dropdown.dataset.originalTagId = tagTile.id;
        } else {
            // Create an ID if it doesn't exist
            tagTile.id = 'tag-tile-' + Math.random().toString(36).substr(2, 9);
            dropdown.dataset.originalTagId = tagTile.id;
        }

        console.log('Moving dropdown to document body, storing tag reference:', tagTile.id);
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
    dropdown.style.opacity = '1';
    dropdown.style.pointerEvents = 'auto'; // Re-enable pointer events when showing
    dropdown.style.zIndex = '999999'; // Ensure it's above everything

    console.log('Positioned and shown dropdown:', dropdownId, 'classes:', dropdown.className);

    console.log('Final positioned dropdown:', dropdownId, 'at', left, top);
    console.log('Tile absolute position:', tileRect.left + scrollLeft, tileRect.bottom + scrollTop);

    // Set up click outside to close (only if not already set up)
    if (!dropdown.hasAttribute('data-click-outside-setup')) {
        dropdown.setAttribute('data-click-outside-setup', 'true');
        const handleClickOutside = (event) => {
            if (!dropdown.contains(event.target) && !tagTile.contains(event.target)) {
                dropdown.classList.remove('show');
                dropdown.classList.add('hide');

                // Move dropdown off-screen and disable pointer events
                dropdown.style.top = '-9999px';
                dropdown.style.left = '-9999px';
                dropdown.style.pointerEvents = 'none';

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
        console.log('Hiding dropdown:', dropdownId, 'current classes:', dropdown.className);

        dropdown.classList.remove('show');
        dropdown.classList.add('hide');

        // Move dropdown off-screen and disable pointer events
        dropdown.style.top = '-9999px';
        dropdown.style.left = '-9999px';
        dropdown.style.pointerEvents = 'none';
        dropdown.style.visibility = 'hidden';
        dropdown.style.opacity = '0';

        console.log('Hidden dropdown:', dropdownId, 'new classes:', dropdown.className);
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

    // Periodic cleanup disabled to prevent interference with reopening
    // startDropdownCleanup();
};

// Global function to ensure all hidden dropdowns are properly disabled
window.cleanupHiddenDropdowns = () => {
    const allDropdowns = document.querySelectorAll('.children-dropdown');
    allDropdowns.forEach(dropdown => {
        // Only cleanup dropdowns that are stuck visible but should be hidden
        // Check if dropdown is visible but not intentionally shown
        const isVisible = dropdown.style.visibility !== 'hidden' &&
                          dropdown.style.opacity !== '0' &&
                          dropdown.style.top !== '-9999px';
        const isIntentionallyShown = dropdown.classList.contains('show');

        // Only intervene if dropdown is visible but not intentionally shown
        if (isVisible && !isIntentionallyShown && dropdown.classList.contains('hide')) {
            // Force disable interaction for stuck dropdowns only
            dropdown.style.pointerEvents = 'none';
            dropdown.style.top = '-9999px';
            dropdown.style.left = '-9999px';
            dropdown.style.visibility = 'hidden';
            dropdown.style.opacity = '0';

            console.log('Cleaned up stuck visible dropdown:', dropdown.id);
        }
    });
};

// Start periodic cleanup (runs every 10 seconds to catch any edge cases without interfering)
window.startDropdownCleanup = () => {
    if (window.dropdownCleanupInterval) {
        clearInterval(window.dropdownCleanupInterval);
    }

    window.dropdownCleanupInterval = setInterval(() => {
        window.cleanupHiddenDropdowns();
    }, 10000);
};

// Stop cleanup when page unloads
window.addEventListener('beforeunload', () => {
    if (window.dropdownCleanupInterval) {
        clearInterval(window.dropdownCleanupInterval);
    }
});

// Scroll to and highlight a specific tag
window.scrollToTag = (canonicalTag) => {
    console.log('Scrolling to tag:', canonicalTag);

    // Find all tag tiles that match the canonical tag
    const tagTiles = document.querySelectorAll('.tag-tile, .child-tag-tile');
    let targetTile = null;

    tagTiles.forEach(tile => {
        // Check if this tile contains the target tag
        // We'll look for data attributes or match against the tile's display text
        const tileImage = tile.querySelector('img');
        if (tileImage) {
            const altText = tileImage.getAttribute('alt');
            const displayName = canonicalTag.replace(/_/g, ' '); // Convert underscores to spaces

            if (altText && (altText.toLowerCase() === displayName.toLowerCase() ||
                           altText.toLowerCase() === canonicalTag.toLowerCase())) {
                targetTile = tile;
            }
        }
    });

    if (targetTile) {
        // Scroll to the tile with smooth animation
        targetTile.scrollIntoView({
            behavior: 'smooth',
            block: 'center',
            inline: 'nearest'
        });

        // Add a highlight effect
        targetTile.classList.add('highlight-target');
        setTimeout(() => {
            if (targetTile.classList) {
                targetTile.classList.remove('highlight-target');
            }
        }, 3000);

        console.log('Found and scrolled to tag tile:', targetTile);
    } else {
        console.log('Tag tile not found for:', canonicalTag);

        // If not found in visible tiles, it might be in a collapsed section
        // Try to expand sections that might contain it
        const expandBtns = document.querySelectorAll('.expand-btn:not(.expanded)');
        expandBtns.forEach(btn => btn.click());

        // Try again after a delay to let sections expand
        setTimeout(() => {
            window.scrollToTag(canonicalTag);
        }, 500);
    }
};