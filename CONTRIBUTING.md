# Contributing to Instance Manager for VRChat

Thanks for contributing.

## Ground rules
- Keep PRs focused and reviewable.
- For user-facing changes, include screenshots or GIFs when possible.
- Please avoid adding telemetry, tracking, or “phone home” behavior.

## How to contribute

### 1) Open an issue (recommended)
- Bugs: steps to reproduce, expected vs actual behavior, logs or screenshots if possible.
- Features: describe the user problem first, then a proposed solution.

### 2) Fork and branch
Use clear branch names:
- `fix/<short-name>`
- `feature/<short-name>`

### 3) Build and test
(Adjust once build steps are finalized)
```bash
dotnet restore
dotnet build
```

### 4) Pull request checklist
- [ ] Explains what changed and why
- [ ] Includes how to test
- [ ] Avoids unrelated formatting-only churn
- [ ] UI changes include screenshots

## License of contributions
By submitting a contribution, you agree your work is provided under the project license (**AGPL-3.0**).
