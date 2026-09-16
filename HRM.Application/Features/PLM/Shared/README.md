# PLM Shared Boundary

This folder contains cross-PLM contracts and authorization decisions shared by Formula, Materials, Manufacturing
Formula and Sample Request features. It is not a replacement for feature-local rules.

`Authorization/PLMFieldVisibilityService` resolves the central PLM capabilities for formula prices, formula materials
and product technical information. It preserves the existing role matrix while allowing the runtime source to move
from compatibility role mapping to Identity permission claims.

Feature handlers remain responsible for endpoint policy, current-company scope, ownership/visibility scope, active
record filtering and DTO mapping. Field visibility is applied after those checks; a hidden field is not authorization
for the underlying record.

