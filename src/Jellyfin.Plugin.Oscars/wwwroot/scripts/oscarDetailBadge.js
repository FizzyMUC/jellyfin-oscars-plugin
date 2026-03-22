(function () {
    'use strict';

    console.log("[Jellyfin Oscars] script loaded");

    var debugEnabled = !!window.__jellyfinOscarsDebug;

    if (window.__jellyfinOscarsInitialized || window.__oscarsBadgeInitialized) {
        if (debugEnabled && window.console && typeof window.console.debug === 'function') {
            window.console.debug('[Jellyfin Oscars]', 'Duplicate bootstrap prevented because Oscar badge script is already initialized.');
        }
        return;
    }

    window.__jellyfinOscarsInitialized = true;
    window.__oscarsBadgeInitialized = true;

    var pluginAssetBase = '/plugins/Jellyfin.Oscars';
    var badgeScriptPath = pluginAssetBase + '/scripts/oscarDetailBadge.js';
    var trophyIconPath = pluginAssetBase + '/images/trophy.png';
    var badgeClassName = 'jellyfinOscarsDetailBadge';
    var badgeSelector = '.' + badgeClassName;
    var renderDelayMs = 120;
    var detailDomRetryDelayMs = 180;
    var maxDetailDomRetries = 15;
    var fetchRetryDelayMs = 300;
    var maxFetchRetries = 8;
    var renderTimer = null;
    var postRenderTimer = null;
    var activeRequestToken = 0;
    var pendingItemId = null;
    var pendingStatus = null;
    var renderedItemId = null;
    var lastRouteKey = null;
    var detailDomRetryCount = 0;
    var fetchRetryCount = 0;
    var postRenderRetryCount = 0;
    var exactMetadataRowSelector = '#itemDetailPage > div.detailPageWrapperContainer > div.detailPagePrimaryContainer > div.detailRibbon.padded-left.padded-right > div.infoWrapper > div.itemMiscInfo.itemMiscInfo-primary';
    var exactMetadataRowRelativeSelector = 'div.detailPageWrapperContainer > div.detailPagePrimaryContainer > div.detailRibbon.padded-left.padded-right > div.infoWrapper > div.itemMiscInfo.itemMiscInfo-primary';
    var activePageElement = null;

    function log(level, message, error) {
        if (level !== 'warn' && level !== 'error' && !debugEnabled) {
            return;
        }

        if (!window.console || typeof window.console[level] !== 'function') {
            return;
        }

        if (error) {
            window.console[level]('[Jellyfin Oscars]', message, error);
            return;
        }

        window.console[level]('[Jellyfin Oscars]', message);
    }

    function injectStyles() {
        if (document.getElementById('jellyfin-oscars-detail-badge-styles')) {
            return;
        }

        var style = document.createElement('style');
        style.id = 'jellyfin-oscars-detail-badge-styles';
        style.textContent = [
            badgeSelector + '{display:inline-flex;align-items:center;vertical-align:middle;white-space:nowrap;font-size:inherit;line-height:inherit;}',
            badgeSelector + '.mediaInfoItem,.oscarsBadge.mediaInfoItem{display:inline-flex;align-items:center;vertical-align:middle;padding:0;font-size:inherit;line-height:inherit;white-space:nowrap;flex:0 0 auto;gap:.25rem;}',
            '[dir=ltr] ' + badgeSelector + '.mediaInfoItem,[dir=ltr] .oscarsBadge.mediaInfoItem{margin:0 .5rem 0 .5rem;}',
            '[dir=rtl] ' + badgeSelector + '.mediaInfoItem,[dir=rtl] .oscarsBadge.mediaInfoItem{margin:0 .5rem 0 .5rem;}',
            badgeSelector + '.mediaInfoText,.oscarsBadge.mediaInfoText{display:inline-flex;align-items:center;margin:0;padding:0;line-height:inherit;font-size:inherit;font-weight:inherit;background:transparent;border-radius:0;white-space:nowrap;color:inherit;}',
            '.' + badgeClassName + '__icon,.oscarsBadge__icon{width:1rem;height:1rem;display:block;flex:0 0 auto;object-fit:contain;margin-right:.25rem;vertical-align:middle;}',
            '.' + badgeClassName + '--winner.mediaInfoText{color:#e1bd68;}',
            '.' + badgeClassName + '--nominated.mediaInfoText{color:#c8c8c8;}',
            '@media (max-width: 640px){' + badgeSelector + '.mediaInfoItem,.oscarsBadge.mediaInfoItem{margin:0 .4rem 0 .4rem;gap:.22rem;}' + '.' + badgeClassName + '__icon,.oscarsBadge__icon{width:.95rem;height:.95rem;margin-right:.18rem;}}'
        ].join('');

        document.head.appendChild(style);
    }

    function getHashSearchParams() {
        var hash = window.location.hash || '';
        var queryIndex = hash.indexOf('?');
        if (queryIndex === -1) {
            return new URLSearchParams();
        }

        return new URLSearchParams(hash.substring(queryIndex + 1));
    }

    function extractItemId() {
        var searchParams = new URLSearchParams(window.location.search || '');
        var hashParams = getHashSearchParams();
        var keys = ['id', 'itemId'];
        var i;

        for (i = 0; i < keys.length; i += 1) {
            if (searchParams.get(keys[i])) {
                return searchParams.get(keys[i]);
            }

            if (hashParams.get(keys[i])) {
                return hashParams.get(keys[i]);
            }
        }

        var routeText = [window.location.pathname || '', window.location.hash || ''].join(' ');
        var routeMatch = routeText.match(/details\/([A-Za-z0-9]+)/i);
        return routeMatch ? routeMatch[1] : null;
    }

    function isDetailPageRoute() {
        var routeText = [window.location.pathname || '', window.location.hash || ''].join(' ');
        return /details/i.test(routeText);
    }

    function getRouteKey() {
        return [window.location.pathname || '', window.location.hash || '', window.location.search || ''].join('|');
    }

    function resolvePageElement(source) {
        if (source instanceof HTMLElement) {
            return source.closest('.page') || source.closest('.itemDetailPage') || source;
        }

        return document.querySelector('#itemDetailPage')
            || document.querySelector('.itemDetailPage')
            || document.querySelector('.page');
    }

    function setActivePageElement(pageElement) {
        activePageElement = resolvePageElement(pageElement);
    }

    function getDetailPageRoot() {
        if (activePageElement instanceof HTMLElement) {
            return activePageElement.querySelector('.detailPagePrimaryContainer')
                || activePageElement.querySelector('.detailPageContent')
                || activePageElement;
        }

        return document.querySelector('#itemDetailPage .detailPagePrimaryContainer')
            || document.querySelector('.itemDetailPage .detailPagePrimaryContainer')
            || document.querySelector('.detailPagePrimaryContainer')
            || document.querySelector('#itemDetailPage')
            || document.querySelector('.itemDetailPage')
            || document.querySelector('.detailPageContent');
    }

    function isDetailDomReady() {
        var detailRoot = getDetailPageRoot();
        var titleElement = detailRoot && (
            detailRoot.querySelector('.itemName')
            || detailRoot.querySelector('.nameContainer .itemName')
        );

        return !!(detailRoot && titleElement);
    }

    function isMovieDetailPageActive() {
        if (!(activePageElement instanceof HTMLElement)) {
            return isDetailPageRoute();
        }

        return activePageElement.id === 'itemDetailPage'
            || activePageElement.classList.contains('itemDetailPage')
            || !!activePageElement.querySelector('.detailPagePrimaryContainer');
    }

    function isEndsAtElement(element) {
        return !!(
            element
            && element instanceof HTMLElement
            && element.classList.contains('endsAt')
            && element.classList.contains('mediaInfoItem')
        );
    }

    function isMetadataInlineItem(element) {
        return !!(
            element
            && element instanceof HTMLElement
            && !element.classList.contains(badgeClassName)
            && (
                element.classList.contains('mediaInfoItem')
                || isEndsAtElement(element)
            )
        );
    }

    function getTitleFallbackContainer() {
        var detailRoot = getDetailPageRoot();
        var titleContainer = detailRoot && (
            detailRoot.querySelector('.itemName')
            || detailRoot.querySelector('.nameContainer .itemName')
        );

        return titleContainer;
    }

    function getFallbackAnchor() {
        var titleContainer = getTitleFallbackContainer();
        if (!titleContainer) {
            return null;
        }

        var nameContainer = titleContainer.closest('.nameContainer');
        if (nameContainer) {
            return nameContainer;
        }

        if (titleContainer.parentElement && titleContainer.parentElement !== getDetailPageRoot()) {
            return titleContainer.parentElement;
        }

        return titleContainer;
    }

    function getMetadataRow() {
        var exactRow = activePageElement instanceof HTMLElement
            ? activePageElement.querySelector(exactMetadataRowRelativeSelector)
            : document.querySelector(exactMetadataRowSelector);
        if (exactRow instanceof HTMLElement) {
            return exactRow;
        }

        var fallbackRow = activePageElement instanceof HTMLElement
            ? activePageElement.querySelector('.itemMiscInfo.itemMiscInfo-primary')
                || activePageElement.querySelector('.itemMiscInfoPrimary')
            : document.querySelector('#itemDetailPage .itemMiscInfo.itemMiscInfo-primary')
                || document.querySelector('#itemDetailPage .itemMiscInfoPrimary')
                || document.querySelector('.detailPagePrimaryContainer .itemMiscInfo.itemMiscInfo-primary')
                || document.querySelector('.detailPagePrimaryContainer .itemMiscInfoPrimary');

        return fallbackRow instanceof HTMLElement ? fallbackRow : null;
    }

    function findEndsAtAnchor(container) {
        if (!container) {
            return null;
        }

        var directChildren = Array.from(container.children || []).filter(function (child) {
            return child instanceof HTMLElement;
        });

        var directChild = directChildren.find(function (child) {
            return child instanceof HTMLElement && isEndsAtElement(child);
        });

        if (directChild) {
            return directChild;
        }

        var nestedMatch = Array.from(container.querySelectorAll('.endsAt.mediaInfoItem')).find(function (child) {
            return child instanceof HTMLElement && isEndsAtElement(child);
        });

        if (!nestedMatch) {
            return null;
        }

        var current = nestedMatch;
        while (current && current.parentElement !== container) {
            current = current.parentElement;
        }

        return current && current.parentElement === container ? current : null;
    }

    function getLastMetadataItem(container) {
        if (!container) {
            return null;
        }

        var items = Array.from(container.children || []).filter(isMetadataInlineItem);
        return items.length ? items[items.length - 1] : null;
    }

    function isElementVisible(element) {
        if (!(element instanceof HTMLElement) || !element.isConnected) {
            return false;
        }

        var style = window.getComputedStyle(element);
        if (style.display === 'none' || style.visibility === 'hidden' || style.opacity === '0') {
            return false;
        }

        if (!element.parentElement) {
            return false;
        }

        var parentStyle = window.getComputedStyle(element.parentElement);
        if (parentStyle.display === 'none' || parentStyle.visibility === 'hidden') {
            return false;
        }

        return element.getClientRects().length > 0;
    }

    function removeExistingBadges() {
        var badges = document.querySelectorAll(badgeSelector);
        badges.forEach(function (badge) {
            badge.remove();
        });
        if (postRenderTimer) {
            clearTimeout(postRenderTimer);
            postRenderTimer = null;
        }
    }

    function resetRetryState() {
        detailDomRetryCount = 0;
        fetchRetryCount = 0;
        postRenderRetryCount = 0;
    }

    function getExistingBadge() {
        if (activePageElement instanceof HTMLElement) {
            return activePageElement.querySelector(badgeSelector);
        }

        return document.querySelector(badgeSelector);
    }

    function getOscarStatus(item) {
        var tags = Array.isArray(item && item.Tags) ? item.Tags : [];
        var normalizedTags = tags.map(function (tag) {
            return String(tag).trim().toLowerCase();
        });

        if (normalizedTags.indexOf('oscar winner') !== -1) {
            return {
                cssClass: badgeClassName + '--winner',
                text: 'Oscar Winner'
            };
        }

        if (normalizedTags.indexOf('oscar nominated') !== -1) {
            return {
                cssClass: badgeClassName + '--nominated',
                text: 'Oscar Nominee'
            };
        }

        return null;
    }

    function createBadge(status) {
        var wrapper = document.createElement('div');
        wrapper.className = 'mediaInfoItem mediaInfoText oscarsBadge ' + badgeClassName + ' ' + status.cssClass;

        var icon = document.createElement('img');
        icon.className = badgeClassName + '__icon';
        icon.alt = '';
        icon.setAttribute('aria-hidden', 'true');
        icon.src = trophyIconPath;

        var text = document.createElement('span');
        text.textContent = status.text;

        wrapper.appendChild(icon);
        wrapper.appendChild(text);
        return wrapper;
    }

    function normalizeBadgeElement(badgeElement) {
        if (!(badgeElement instanceof HTMLElement)) {
            return createBadge(pendingStatus);
        }

        var legacyContent = badgeElement.querySelector('.mediaInfoText');
        if (legacyContent && legacyContent !== badgeElement) {
            while (legacyContent.firstChild) {
                badgeElement.appendChild(legacyContent.firstChild);
            }
            legacyContent.remove();
        }

        var iconElement = badgeElement.querySelector('img');
        if (!iconElement) {
            iconElement = document.createElement('img');
            badgeElement.insertBefore(iconElement, badgeElement.firstChild);
        }

        iconElement.className = badgeClassName + '__icon';
        iconElement.alt = '';
        iconElement.setAttribute('aria-hidden', 'true');
        iconElement.src = trophyIconPath;

        var textElement = badgeElement.querySelector('span');
        if (!textElement) {
            textElement = document.createElement('span');
            badgeElement.appendChild(textElement);
        }

        textElement.textContent = pendingStatus.text;

        Array.from(badgeElement.children).forEach(function (child) {
            if (child !== iconElement && child !== textElement) {
                child.remove();
            }
        });

        if (badgeElement.firstElementChild !== iconElement) {
            badgeElement.insertBefore(iconElement, badgeElement.firstChild);
        }

        if (iconElement.nextElementSibling !== textElement) {
            badgeElement.appendChild(textElement);
        }

        return badgeElement;
    }

    function ensureBadgeElement() {
        var badgeElement = getExistingBadge();
        if (!badgeElement) {
            badgeElement = createBadge(pendingStatus);
        }

        badgeElement = normalizeBadgeElement(badgeElement);
        badgeElement.className = 'mediaInfoItem mediaInfoText oscarsBadge ' + badgeClassName + ' ' + pendingStatus.cssClass;
        return badgeElement;
    }

    function placeBadgeInFallback(badgeElement) {
        var anchorElement = getFallbackAnchor();
        if (!anchorElement || !anchorElement.parentElement) {
            log('warn', 'Oscar badge fallback placement failed because no title container was found.');
            return false;
        }

        anchorElement.insertAdjacentElement('afterend', badgeElement);
        return true;
    }

    function placeBadgeInMetadataRow(badgeElement) {
        var metadataRow = getMetadataRow();
        if (!metadataRow) {
            return false;
        }

        var endsAtAnchor = findEndsAtAnchor(metadataRow);
        if (endsAtAnchor) {
            metadataRow.insertBefore(badgeElement, endsAtAnchor);
            return true;
        }

        var lastMetadataItem = getLastMetadataItem(metadataRow);
        if (lastMetadataItem) {
            lastMetadataItem.insertAdjacentElement('afterend', badgeElement);
            return true;
        }

        metadataRow.appendChild(badgeElement);
        return true;
    }

    function verifyRenderedBadge(itemId) {
        if (postRenderTimer) {
            clearTimeout(postRenderTimer);
        }

        postRenderTimer = window.setTimeout(function () {
            postRenderTimer = null;

            if (itemId !== pendingItemId || itemId !== extractItemId()) {
                return;
            }

            var badgeElement = getExistingBadge();
            if (!badgeElement) {
                log('warn', 'Post-render verification failed: badge element was removed from the DOM.');
            }
            else if (!badgeElement.parentElement) {
                log('warn', 'Post-render verification failed: badge parent is missing.');
            }
            else if (!isElementVisible(badgeElement)) {
                log('warn', 'Post-render verification failed: badge is present but not visible.');
            } else {
                log('debug', 'badge visible in final DOM for item ' + itemId + '.');
                return;
            }

            if (postRenderRetryCount < 1) {
                postRenderRetryCount += 1;
                renderedItemId = null;
                removeExistingBadges();
                scheduleRender(renderDelayMs);
                return;
            }
        }, 160);
    }

    function tryRenderBadge() {
        if (!pendingStatus || !pendingItemId || extractItemId() !== pendingItemId) {
            resetRetryState();
            renderedItemId = null;
            removeExistingBadges();
            return false;
        }

        if (renderedItemId === pendingItemId && getExistingBadge()) {
            log('debug', 'Oscar badge render skipped for item ' + pendingItemId + ' because it is already present.');
            return true;
        }

        var badgeElement = ensureBadgeElement();
        if (!placeBadgeInMetadataRow(badgeElement) && !placeBadgeInFallback(badgeElement)) {
            return false;
        }

        renderedItemId = pendingItemId;
        log('debug', 'Oscar badge rendered for item ' + pendingItemId + '.');
        verifyRenderedBadge(pendingItemId);
        return true;
    }

    function fetchItem(itemId) {
        return new Promise(function (resolve, reject) {
            if (!window.ApiClient || typeof window.ApiClient.getCurrentUserId !== 'function') {
                reject(new Error('ApiClient is not available.'));
                return;
            }

            var userId = window.ApiClient.getCurrentUserId();
            if (!userId) {
                reject(new Error('No current Jellyfin user is available.'));
                return;
            }

            window.ApiClient.ajax({
                type: 'GET',
                url: '/Users/' + encodeURIComponent(userId) + '/Items/' + encodeURIComponent(itemId),
                dataType: 'json'
            }).then(resolve).catch(reject);
        });
    }

    function renderCurrentPageBadge() {
        clearTimeout(renderTimer);
        renderTimer = null;

        if (!isMovieDetailPageActive()) {
            pendingItemId = null;
            pendingStatus = null;
            renderedItemId = null;
            lastRouteKey = null;
            resetRetryState();
            removeExistingBadges();
            return;
        }

        if (!isDetailDomReady()) {
            if (detailDomRetryCount < maxDetailDomRetries) {
                detailDomRetryCount += 1;
                scheduleRender(detailDomRetryDelayMs);
            }

            return;
        }

        detailDomRetryCount = 0;
        var itemId = extractItemId();
        if (!itemId) {
            pendingItemId = null;
            pendingStatus = null;
            renderedItemId = null;
            resetRetryState();
            removeExistingBadges();
            if (detailDomRetryCount < maxDetailDomRetries) {
                detailDomRetryCount += 1;
                scheduleRender(detailDomRetryDelayMs);
            }
            return;
        }

        var currentRouteKey = getRouteKey();
        if (lastRouteKey !== currentRouteKey) {
            lastRouteKey = currentRouteKey;
            pendingStatus = null;
            pendingItemId = itemId;
            renderedItemId = null;
            resetRetryState();
            removeExistingBadges();
        }

        if (pendingItemId !== itemId) {
            pendingStatus = null;
            pendingItemId = itemId;
            renderedItemId = null;
            resetRetryState();
            removeExistingBadges();
        }

        if (pendingItemId === itemId && pendingStatus) {
            if (tryRenderBadge()) {
                return;
            }
        }

        var requestToken = activeRequestToken + 1;
        activeRequestToken = requestToken;

        fetchItem(itemId).then(function (item) {
            if (requestToken !== activeRequestToken) {
                return;
            }

            if (!item || item.Type !== 'Movie') {
                pendingItemId = null;
                pendingStatus = null;
                renderedItemId = null;
                lastRouteKey = currentRouteKey;
                resetRetryState();
                removeExistingBadges();
                return;
            }

            var status = getOscarStatus(item);
            if (!status) {
                pendingItemId = null;
                pendingStatus = null;
                renderedItemId = null;
                lastRouteKey = currentRouteKey;
                resetRetryState();
                removeExistingBadges();
                log('debug', 'Oscar badge skipped for item ' + itemId + ' because no Oscar tag is present.');
                return;
            }

            pendingItemId = itemId;
            pendingStatus = status;
            lastRouteKey = currentRouteKey;
            fetchRetryCount = 0;
            tryRenderBadge();
        }).catch(function (error) {
            if (requestToken !== activeRequestToken) {
                return;
            }

            pendingItemId = null;
            pendingStatus = null;
            renderedItemId = null;
            removeExistingBadges();
            log('warn', 'Unable to render Oscar detail badge.', error);

            if (isDetailPageRoute() && fetchRetryCount < maxFetchRetries) {
                fetchRetryCount += 1;
                scheduleRender(fetchRetryDelayMs);
                return;
            }
            resetRetryState();
        });
    }

    function scheduleRender(delayMs) {
        clearTimeout(renderTimer);
        renderTimer = window.setTimeout(renderCurrentPageBadge, typeof delayMs === 'number' ? delayMs : renderDelayMs);
    }

    function init() {
        injectStyles();
        log('debug', 'Bootstrap initialized from ' + badgeScriptPath + '.');
        setActivePageElement(document.querySelector('#itemDetailPage') || document.querySelector('.itemDetailPage') || document.querySelector('.page'));
        scheduleRender();

        window.addEventListener('load', function () {
            setActivePageElement(document.querySelector('#itemDetailPage') || document.querySelector('.itemDetailPage') || document.querySelector('.page'));
            scheduleRender();
        });
        document.addEventListener('pagebeforeshow', function (event) {
            setActivePageElement(event.target);
            scheduleRender();
        }, true);
        document.addEventListener('pageshow', function (event) {
            setActivePageElement(event.target);
            scheduleRender();
        }, true);
    }

    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', init, { once: true });
    }
    else {
        init();
    }
}());
