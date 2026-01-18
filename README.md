## DB-Driven Localization for ASP.NET Core 10 MVC (Cached + Versioned)

Overview
This project implements a production-grade, database-driven localization system for an ASP.NET Core 10 MVC application. All translations are stored in SQL Server (no RESX files). Razor views stay simple by calling a key-based helper (Localize) directly in .cshtml. Performance is optimized by loading all translation keys into memory per culture and serving lookups from dictionaries. The cache is refreshed only when a database version number changes (version invalidation), and version checks are throttled using a configurable interval (VersionCheckSeconds) to avoid constant DB access.

Key Goals

1. Database-driven localization (no RESX): translations managed in SQL and can be updated without rebuilding the app.
2. Fast rendering: zero DB calls per key lookup; view rendering uses in-memory dictionary lookups.
3. Safe cache refresh: cache reloads only when translation data changes (version invalidation).
4. Simple Razor usage: views call Localize("Key") or Localize("Key", args) without injecting services into every view.
5. Security: Localize output is safe by default (Razor encoding). LocalizeRaw exists only as an explicit opt-in for trusted HTML.
6. Production-friendly: clean architecture, minimal files, and GitHub-ready.

How to Use in Razor Views
In any .cshtml file, use the helper methods exposed via the base view page:

* Localize("Home.Title")
* Localize("Welcome.User", Model.Name)

Localize returns a string. Razor encodes output by default, so it is safe against HTML injection.
If you intentionally store trusted HTML in the localization table and want to render it as markup, use LocalizeRaw("Some.HtmlKey"). LocalizeRaw must be used only for content you fully control.

Database Model
The solution is based on three core tables:

1. App_Language
   Stores the list of supported UI languages.
   Typical columns:

* Id
* Name (English, Arabic (Kuwait), Urdu, Telugu, etc.)
* Culture (en-US, ar-KW, ur, te)
* CultureNormalized (lowercase normalized culture string)
* IsActive
  This table defines which cultures are available to the application.

2. App_LanguageResource
   Stores translations by language and key.
   Typical columns:

* Id
* LanguageId (FK to App_Language)
* ResourceKey (e.g., "Home.Title")
* ResourceKeyNormalized (lowercase, trimmed)
* Value (translated string)
* IsActive
  A unique index exists on (LanguageId, ResourceKeyNormalized) to ensure no duplicate keys per language. This allows safe updates and fast lookups during preload.

3. App_LocalizationVersion
   Stores a single version number used for cache invalidation.
   Typical columns:

* Id (usually 1)
* VersionNumber (INT)
  Whenever translations change, VersionNumber is incremented. The application compares this number with its in-memory version to determine when to reload the cache.

Core Runtime Design
The runtime design is based on a single concept: translations must be served from memory, and the memory cache must refresh only when the database indicates data has changed.

1. In-memory cache structure
   At startup, all active translations are loaded into memory into a nested dictionary:

* Outer dictionary: Culture -> Inner dictionary
* Inner dictionary: ResourceKeyNormalized -> Value

In simplified terms:
Cache[culture][key] = translation

This makes each lookup an O(1) dictionary read.

2. Lookup flow (fast path)
   When a view calls Localize("Home.Title"):

* The current UI culture is read from CultureInfo.CurrentUICulture (set by RequestLocalization middleware).
* The cache dictionary for that culture is selected.
* The key is normalized (trim + lower) and looked up in the inner dictionary.
* If found, the translation is returned.
* If not found, the key itself is returned as a safe fallback.

This lookup does not query the database. Therefore, the number of translated strings on a page does not increase database load.

3. Missing key behavior
   If a key is missing for a culture, the system returns the key string itself, e.g., "Home.Title". This avoids breaking pages and makes missing translations visible during testing.

Version Invalidation (Cache Reload Only When Data Changes)
Caching is only useful if it can refresh correctly. This project uses version invalidation.

How it works:

* The application stores the last loaded VersionNumber in memory.
* A lightweight query reads the current VersionNumber from App_LocalizationVersion.
* If the number changed since last load, the entire translation set is reloaded into memory once and the in-memory version is updated.
* If the number did not change, no reload occurs.

How version is bumped:

* A stored procedure App_Localization_BumpVersion increments VersionNumber.
* Optionally, a database trigger can be enabled on App_LanguageResource to automatically bump VersionNumber on INSERT, UPDATE, or DELETE. This makes cache refresh automatic and removes the need to manually call the procedure.

Why this is reliable:

* Reload happens only when translations actually change.
* Reload is a controlled operation, executed once per version change, not per request.

VersionCheckSeconds (Why It Exists and What It Does)
VersionCheckSeconds is a throttle setting to prevent excessive database access for version checking.

The configuration is in appsettings.json:
DbLocalization:

* DefaultCulture
* CultureCookieName
* VersionCheckSeconds

Example:
DefaultCulture = "en-US"
CultureCookieName = ".App.Culture"
VersionCheckSeconds = 60

What it means:

* The app will not check the database VersionNumber more than once per 60 seconds per app instance.
* During the 60-second window, all localization lookups use in-memory cache only.
* After the window expires, the next request triggers a small version check query.
* If the version changed, cache reload happens; otherwise nothing happens.

Why this is important:
Without throttling, checking version on every request can create unnecessary database load under traffic. With VersionCheckSeconds, the system remains scalable because only a small number of version queries occur regardless of how many pages are being rendered.

How the value is read from appsettings.json:
At startup, Program.cs binds the configuration section "DbLocalization" to a strongly typed options class (DbLocalizationOptions). The value 60 is then available through injected options in the localization cache store and services. The localization cache store uses this value to decide when it is allowed to re-check the database version.

When version checking occurs:

* Not on every lookup.
* Not on every request.
* Only when the elapsed time since the last version check exceeds VersionCheckSeconds.

Culture Switching (How Language Changes)
The application uses ASP.NET Core RequestLocalization middleware and a cookie-based culture provider.

1. User selects a language (from the LanguageSwitcher UI).
2. The /culture/set endpoint sets a cookie that contains the selected culture.
3. On the next request, RequestLocalization reads the cookie and sets:

* CultureInfo.CurrentCulture
* CultureInfo.CurrentUICulture

4. Views render using the new culture automatically because Localize reads CultureInfo.CurrentUICulture.

Cookie name:
The cookie name is configurable via DbLocalization:CulturalCookieName (e.g., ".App.Culture").

Default culture:
If no cookie is present, the app uses DbLocalization:DefaultCulture (e.g., "en-US").

RTL Support
The layout checks the current UI culture. If the culture is Arabic/Urdu (or other RTL languages), it sets the HTML dir attribute to "rtl". This allows Bootstrap and the UI to correctly reflect RTL layout.

Security Notes

* Localize output is safe by default because Razor encodes strings.
* LocalizeRaw should only be used for trusted HTML content controlled by the developer.
* Do not store untrusted HTML in localization tables.

Performance Notes

* Key lookups are dictionary reads, not database queries.
* Cache reload is rare and controlled by version changes.
* Version checks are throttled by VersionCheckSeconds to reduce database load.
* The solution scales well because translation volume affects memory usage rather than request latency or database traffic.

Deployment / Multi-Instance Notes
Each app instance maintains its own in-memory cache. When translations change and VersionNumber is bumped, each instance will refresh within its VersionCheckSeconds window. This avoids requiring distributed caching for correctness. If faster refresh is required, reduce VersionCheckSeconds or enable an automated trigger-based bump strategy.

Project Structure Summary

* Razor base view page exposes Localize/LocalizeRaw helpers to all views via _ViewImports.
* LocalizationCacheStore loads resources, holds the cache, and handles version checks and reload.
* LocalizationService uses the cache store to return translations quickly.
* LanguageService returns active languages for UI switcher and culture configuration.
* CultureController sets culture cookie via /culture/set.
* LanguageSwitcher view component displays the culture selection UI.

How to Update Translations

1. Insert/update/delete records in App_LanguageResource for your target language.
2. Bump version:

* Either run App_Localization_BumpVersion manually
* Or enable the trigger to bump automatically

3. The app will reload cache when it detects version change.

Author
Ashraf Ali
Senior Technical Lead | Senior .NET Full-Stack Developer | Solution Architect
.NET Architecting & Delivery | Microservices | REST APIs | SQL Server | Fintech Integrations | JS | jQ | BS | 27+ Applications
LinkedIn : https://www.linkedin.com/in/ashraf-ali-b43aa321a/
Portfolio : https://ashraf712.github.io/
