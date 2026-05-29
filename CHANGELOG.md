## 1.1.0 (2026-05-29)

#### Feature

* **ci:** changelog automation (#15) (a6b9a2a8)
* java native runtime artifacts (117c04ee)
* python/dotnet interchangable app (7ff793a0)

#### Bug Fixes

* **java:** use spring vault (#14) (ddff1245)
* **ci:** dedicated python workflow (423d02ba)
* **c:** dedicated dotnet workflow (53873076)
* **c:** dedicated dotnet workflow (93eb0448)
* update merge from main (22169c9e)
* java tls insecure connection (5421ea43)
* autocommit disable (69b149ba)
* also build java as container (c450f695)
* update jobs and definitions (afd8382f)

#### Code Refactoring

* **dotnet:** use VaultSharp for transit and dynamic DB credentials (cc8cb417)
* **python:** use hvac adapter for transform requests (c8d4aa45)

#### Chores

* **dotnet:** add VaultSharp dependency for Vault SDK migration (24de4b65)
* **deps:** bump requests from 2.32.3 to 2.33.0 in /app/python (9e808f73)
* **deps:** bump flask from 3.0.3 to 3.1.3 in /app/python (849d7fee)
* **deps:** bump mysql-connector-python in /app/python (f957139f)
* ignore java build output (ac7ccd3e)


## 1.0.8 (2026-03-04)

#### Bug Fixes

* update workflow (d14df577)


## 1.0.7 (2024-10-29)

#### Bug Fixes

* refactoring code (2848b12f)

#### Code Refactoring

* Cleanup of code using pylint and code reviews (b0345ad2)


## 1.0.6 (2024-10-25)

#### Bug Fixes

* pylint review, nomad jobfile update (b435ed2f)


## 1.0.5 (2024-10-25)

#### Bug Fixes

* ref to secret as dynamic value (76873f54)


## 1.0.4 (2024-10-25)

#### Bug Fixes

* update docker image reference (5f984f52)
* Refactoring implementation and Cleanup (f5728c57)


## 1.0.3 (2024-10-24)

#### Bug Fixes

* refactoring example (5fc3be6e)


## 1.0.2 (2024-10-22)

#### Bug Fixes

* update and cleanup setup scripts (bbd50ade)


## 1.0.1 (2024-10-22)

#### Bug Fixes

* update nomad jobs (3cf8cb9b)


## 1.0.0 (2024-09-25)

#### Bug Fixes

* downgrade score to minimum (4c2bbdc2)
* adding container ci template (0712dce5)

