# Simple risk portal flow

This revision replaces the combined RiskScoring controller and its older Add Band workflow.
Calculation stays on the document's 0–100 scale. No business score values or decision
ranges were changed by this UI refactor.

## Two controllers

| Controller | Action | Purpose |
| --- | --- | --- |
| RiskScoresController | Index | Read the selected category, ten rows per page |
| RiskScoresController | Add GET / POST | Open the score form / save the submitted score |
| RiskScoresController | Edit GET / POST | Load a score / save its changes |
| RiskScoresController | Delete POST | Soft-delete a score in pending changes |
| RiskScoreSetupController | Index | Display all ranges with created/updated user and date |
| RiskScoreSetupController | Edit GET / POST | Open and save all ranges together |
| Both controllers | Apply POST | Apply reviewed changes; retain history in the service |

The service owns database queries, client scoping, calculation, validation,
concurrency checks, history and auditing. Controllers handle requests, validate
forms, call the service and return a view/redirect. Unexpected errors are logged
and the user gets a safe message.

## Direct pagination

Open /RiskScores/Index: the controller requests page 1, page size 10.
There is no search submission and no page-size dropdown.
Page 1 reads rows 1–10; page 2 reads 11–20, through database Skip/Take.
The footer displays numbered links plus Previous/Next when there is more than one page.
No pager is shown for an empty category. Switching category returns to its first page.
Category is carried in each page link. Incoming pageSize/search parameters are ignored.

## Screens

- Risk Scores: category tabs, S.N., value/range, score, Edit/Delete, and Add score.
- Risk Score Setup: type, From, To, Remarks, Created By/Date and Updated By/Date.
- Edit ranges: one table to change adjoining boundaries together.
- Version, weight and approval columns are removed from everyday lists.
- An empty validation summary no longer displays a red box.
- A short pending-changes message remains when needed, with Apply reviewed changes.
  Removing that control would remove the document's controlled-change behavior.
  It is still not independent maker/checker approval: a Manage user may save and apply.

## Missing configuration

The last review found no RiskFactors, RiskFactorRules or RiskModels, plus one
MEDIUM 0–10 draft band. This refactor does not overwrite that data or invent country scores.

To enable category tabs and manual score entry, review and run
Database/RiskScoring.CategoriesOnly.sql in SSMS for the correct ClientId.
It inserts only missing categories, with UAT template weights.
It does NOT approve a model, insert scores or repair existing ranges.
An approved complete model, weights, factor rules and 0–100 ranges are still needed
before calculation can work. Template weights are not approved live policy.

## Routes and menu

New lists: /RiskScores/Index and /RiskScoreSetup/Index.
Old links /RiskScoring/Index and /RiskScoring/Setup still resolve to the new controllers.
Run the updated Database/RiskScoring.SplitMenus.sql manually to store new controller
names in MenuItems; restart the app to clear cached menu links.
Close old Add Risk Band/Edit Band inner tabs; Edit ranges replaces those forms.
Old POST forms are not forwarded, so stale submissions cannot silently modify data.

## Local check

1. Restart the rebuilt app; sign in with risk View and Manage permissions.
2. Confirm database setup; open Risk Scores, select a tab and add a test score.
3. With more than ten scores, verify page 2 works without submitting a search.
4. Edit/delete a score, review pending changes, then apply.
5. Open Risk Score Setup → Edit ranges, change adjoining ranges and save.
6. A gap/overlap must reject the whole save. A valid save remains pending until applied.

This refactor is not production-readiness certification. Business approval,
independent checker controls, API integration, complete evidence and deployment
checks remain separate work.

## Verification

- Release compilation: zero warnings and errors.
- 26 local SQL Server checks passed, including ten-row paging, no duplicate rows
  between pages, last-page bounds, atomic range editing, stale-form rejection,
  rejected gaps leaving the draft unchanged, tenant isolation and existing CRUD.
- 11 MVC endpoint checks passed: both new list routes, old list aliases,
  score actions, range actions and removal of the combined controller.
- Temporary test client and test risk/audit rows were removed. Business-client
  data and the manual setup scripts were not changed/executed by these tests.
- An authenticated browser walkthrough of the updated screens remains for local UAT.
