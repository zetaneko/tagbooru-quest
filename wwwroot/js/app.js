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
    console.log('positionDropdown called with ID:', dropdownId);

    // Debug: List all dropdown elements in DOM
    const allDropdowns = document.querySelectorAll('[id^="dropdown-"]');
    console.log('All dropdown elements found:', Array.from(allDropdowns).map(el => el.id));

    const dropdown = document.getElementById(dropdownId);
    if (!dropdown) {
        console.log('Dropdown not found:', dropdownId);
        console.log('Searching for partial matches...');
        const partialMatches = Array.from(allDropdowns).filter(el =>
            el.id.includes(dropdownId.replace('dropdown-', '')) ||
            dropdownId.includes(el.id.replace('dropdown-', ''))
        );
        console.log('Partial matches:', partialMatches.map(el => el.id));
        return;
    }

    // Determine dropdown type and handle positioning accordingly
    const isNestedDropdown = dropdown.classList.contains('nested-children-dropdown') ||
                            dropdown.classList.contains('recursive-children-dropdown');

    if (isNestedDropdown) {
        return positionNestedDropdown(dropdown, dropdownId);
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

// Position nested/recursive dropdowns with depth-based positioning logic
window.positionNestedDropdown = (dropdown, dropdownId) => {
    console.log('Positioning nested dropdown:', dropdownId);

    // Clear any previous hiding state and reset all properties
    dropdown.classList.remove('hide');
    dropdown.classList.add('show');
    dropdown.style.visibility = '';
    dropdown.style.opacity = '';

    // Find the parent tile for nested dropdowns
    let parentTile = dropdown.parentNode.querySelector('.child-tag-tile, .recursive-tag-tile');
    if (!parentTile) {
        // Try looking in the previous sibling or parent container
        const parentContainer = dropdown.closest('.child-tag-container, .recursive-tag-container');
        if (parentContainer) {
            parentTile = parentContainer.querySelector('.child-tag-tile, .recursive-tag-tile');
        }
    }

    if (!parentTile) {
        console.log('Parent tile not found for nested dropdown:', dropdownId);
        return;
    }

    // Extract depth from dropdown class
    let depth = 1;
    const depthMatch = dropdown.className.match(/depth-(\d+)/);
    if (depthMatch) {
        depth = parseInt(depthMatch[1]);
    }

    console.log('Nested dropdown depth:', depth);

    // Get parent tile position
    const parentRect = parentTile.getBoundingClientRect();
    const scrollLeft = window.pageXOffset || document.documentElement.scrollLeft;
    const scrollTop = window.pageYOffset || document.documentElement.scrollTop;

    // Position off-screen first to measure dimensions
    dropdown.style.visibility = 'hidden';
    dropdown.style.opacity = '1';
    dropdown.style.position = 'absolute';
    dropdown.style.top = '0px';
    dropdown.style.left = '0px';

    const dropdownRect = dropdown.getBoundingClientRect();

    console.log('Parent tile rect:', parentRect);
    console.log('Nested dropdown rect:', dropdownRect);

    // Calculate position based on depth and viewport constraints
    const margin = 15;
    const viewportWidth = window.innerWidth;
    const viewportHeight = window.innerHeight;

    let left, top;

    // Horizontal positioning: alternate between right and left expansion based on depth and space
    const rightSpaceAvailable = viewportWidth - (parentRect.right + scrollLeft) - margin;
    const leftSpaceAvailable = (parentRect.left + scrollLeft) - margin;

    // For even depths (2, 4, 6...) or when right space is sufficient, expand right
    // For odd depths (3, 5, 7...) or when left space is more available, expand left
    if ((depth % 2 === 0 && rightSpaceAvailable >= dropdownRect.width) ||
        (rightSpaceAvailable >= leftSpaceAvailable && rightSpaceAvailable >= dropdownRect.width)) {
        // Expand to the right
        left = parentRect.right + scrollLeft + 5; // 5px gap from parent
    } else if (leftSpaceAvailable >= dropdownRect.width) {
        // Expand to the left
        left = parentRect.left + scrollLeft - dropdownRect.width - 5;
    } else {
        // Not enough space on either side, choose the side with more space
        if (rightSpaceAvailable >= leftSpaceAvailable) {
            left = parentRect.right + scrollLeft + 5;
        } else {
            left = parentRect.left + scrollLeft - dropdownRect.width - 5;
        }
    }

    // Vertical positioning: align with parent top, but adjust if goes off screen
    top = parentRect.top + scrollTop;

    // Adjust if dropdown goes off bottom of viewport
    const topRelativeToViewport = top - scrollTop;
    if (topRelativeToViewport + dropdownRect.height > viewportHeight - margin) {
        // Move up so bottom aligns with viewport bottom
        top = scrollTop + viewportHeight - dropdownRect.height - margin;

        // Ensure it doesn't go above viewport
        if (top < scrollTop + margin) {
            top = scrollTop + margin;
        }
    }

    // Final bounds checking for horizontal position
    const leftRelativeToViewport = left - scrollLeft;
    if (leftRelativeToViewport < margin) {
        left = scrollLeft + margin;
    } else if (leftRelativeToViewport + dropdownRect.width > viewportWidth - margin) {
        left = scrollLeft + viewportWidth - dropdownRect.width - margin;
    }

    // Apply position and make visible
    dropdown.style.left = left + 'px';
    dropdown.style.top = top + 'px';
    dropdown.style.visibility = 'visible';
    dropdown.style.opacity = '1';
    dropdown.style.pointerEvents = 'auto';
    dropdown.style.zIndex = (999999 + depth).toString(); // Higher z-index for deeper levels

    console.log('Positioned nested dropdown at depth', depth, 'at', left, top);

    // Set up click outside to close nested dropdowns
    if (!dropdown.hasAttribute('data-click-outside-setup')) {
        dropdown.setAttribute('data-click-outside-setup', 'true');
        const handleNestedClickOutside = (event) => {
            // Check if click is outside all related dropdowns and tiles in the chain
            let clickedInsideChain = false;

            // Walk up the parent chain to see if click is in any related element
            let current = dropdown;
            while (current) {
                if (current.contains(event.target)) {
                    clickedInsideChain = true;
                    break;
                }

                // Check parent tile
                const currentParentContainer = current.closest('.child-tag-container, .recursive-tag-container');
                if (currentParentContainer && currentParentContainer.contains(event.target)) {
                    clickedInsideChain = true;
                    break;
                }

                // Move to next level up
                current = currentParentContainer ? currentParentContainer.closest('.nested-children-dropdown, .recursive-children-dropdown') : null;
            }

            if (!clickedInsideChain) {
                // Close this dropdown and all nested children
                closeNestedDropdownChain(dropdown);

                // Notify Blazor
                const dropdownIdParts = dropdownId.split('dropdown-')[1];
                if (dropdownIdParts && window.blazorDropdownClosed) {
                    window.blazorDropdownClosed(dropdownIdParts);
                }

                document.removeEventListener('click', handleNestedClickOutside);
                dropdown.removeAttribute('data-click-outside-setup');
            }
        };

        setTimeout(() => {
            document.addEventListener('click', handleNestedClickOutside);
        }, 100);
    }
};

// Helper function to close a nested dropdown and all its children
window.closeNestedDropdownChain = (dropdown) => {
    if (!dropdown) return;

    // Close all nested children first
    const childDropdowns = dropdown.querySelectorAll('.nested-children-dropdown, .recursive-children-dropdown');
    childDropdowns.forEach(child => {
        child.classList.remove('show');
        child.classList.add('hide');
        child.style.top = '-9999px';
        child.style.left = '-9999px';
        child.style.pointerEvents = 'none';
        child.style.visibility = 'hidden';
        child.style.opacity = '0';
    });

    // Close the dropdown itself
    dropdown.classList.remove('show');
    dropdown.classList.add('hide');
    dropdown.style.top = '-9999px';
    dropdown.style.left = '-9999px';
    dropdown.style.pointerEvents = 'none';
    dropdown.style.visibility = 'hidden';
    dropdown.style.opacity = '0';
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
    const allDropdowns = document.querySelectorAll('.children-dropdown, .nested-children-dropdown, .recursive-children-dropdown');
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