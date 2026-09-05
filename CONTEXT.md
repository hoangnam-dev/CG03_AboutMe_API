# CG03 AboutMe Portfolio

CG03 AboutMe is a single-Portfolio, bilingual public site with an authenticated administration surface. This glossary fixes the domain language used by specifications, API contracts, tests, and implementation.

## Language

**Portfolio**:
The single public professional profile and its related content in this MVP.
_Avoid_: Tenant, customer portfolio, timeline

**Administrator**:
An ASP.NET Core Identity account assigned the `Admin` role and permitted to manage the Portfolio.
_Avoid_: Owner, Supabase user, public user

**Portfolio Content**:
Administrator-managed Profile, About, Skill, Experience, Project, Certificate, and Resume data that can be exposed publicly.
_Avoid_: Timeline entry, post

**Draft**:
Portfolio Content whose `is_published` state is false and is unavailable through public APIs.
_Avoid_: Hidden content

**Published**:
Portfolio Content whose `is_published` state is true, has the required English and Vietnamese translations, and is eligible for public projection.
_Avoid_: Active

**Locale**:
The requested content language, exactly `en` or `vi`; `en` is the default when no locale is supplied.
_Avoid_: Language fallback

**Translation**:
Locale-specific Portfolio Content stored in a corresponding `*_translations` table.
_Avoid_: Localized copy embedded in a base table

**Limited Disclosure Project**:
A professional Project whose public projection omits contractually sensitive fields even when the Project is Published.
_Avoid_: Private project, frontend-hidden project

**Resume Version**:
An immutable uploaded Resume file identified by a server-allocated year and sequence within one language.
_Avoid_: CV revision supplied by the client

**Active Resume**:
The single Resume Version per language selected for the public current-CV endpoint.
_Avoid_: Published Resume

**Contact Message**:
A public visitor submission persisted for Administrator review; notification is a secondary side effect.
_Avoid_: Email, chat message
