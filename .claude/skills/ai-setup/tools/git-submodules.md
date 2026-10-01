# Git submodules

`src/Submodules/MLock` is a submodule; `src/Packages/manifest.json` references
`com.migsweb.mlock` from `file:../Submodules/MLock/...`. Without it Unity fails package
resolution and nothing compiles (so no `.sln` is generated either).

## Check
```bash
git submodule status          # a leading "-" means not initialised, "+" means wrong commit
```

## Install / repair (all platforms)
```bash
git submodule update --init --recursive
```

## Configure
Optional, so future `git pull` keeps it in sync:
```bash
git config submodule.recurse true
```

## Verify
`git submodule status` lists `src/Submodules/MLock` with no leading `-` or `+`, and
`src/Submodules/MLock/src/mlock-unity-project/Packages/MLock/package.json` exists.
