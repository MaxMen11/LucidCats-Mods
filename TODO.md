# TODO

- [ ] First releases: every release line (MODDING.md section 5) is still at zero.
- [ ] Automate mod/runtime/bundle releases. CI has no game DLLs, so pick one: commit built `dist/` ZIPs and let the tag workflow attach them, a local `gh release create` step, or stripped reference assemblies in a private repo for real CI builds.
- [ ] `all-mods` bundle: reserved, nothing builds it yet.
- [ ] `release.yml`: mod/runtime/bundle tags skip silently; print a notice or fail. Source-library ZIPs need `--prefix="$NAME/"` on `git archive` to extract as one folder.
- [ ] PRs only get `check_workspace.py`; a compile check needs the reference-assembly route.
- [ ] `check.yml` runs twice per PR; limit the `push` trigger to `main`.
- [ ] Runtime template: `lowerName` symbol missing, so `dotnet new lcruntime` leaves `com.lucidcats.runtimename` unsubstituted.
- [ ] `dist/mods/.gitkeep` and `dist/runtime/.gitkeep`: whitelisted in `.gitignore` but missing.
- [ ] ZIPs are not byte-reproducible (timestamps inside); `Touch` staged files before `ZipDirectory`. Only matters if `dist/` ZIPs get committed.
