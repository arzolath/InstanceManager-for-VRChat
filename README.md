<div align="center">

  # Instance Manager for VRChat

  Automatically remove unwanted players from your VRChat instance.

  <p>
    <a href="../../releases">
      <img src="https://img.shields.io/github/v/release/arzolath/InstanceManager-for-VRChat?display_name=tag" alt="Release">
    </a>
    <a href="../../actions">
      <img src="https://img.shields.io/github/actions/workflow/status/arzolath/InstanceManager-for-VRChat/build.yml" alt="Build">
    </a>
    <a href="../../issues">
      <img src="https://img.shields.io/github/issues/arzolath/InstanceManager-for-VRChat" alt="Issues">
    </a>
    <a href="./LICENSE">
      <img src="https://img.shields.io/badge/License-AGPLv3-blue.svg" alt="License: AGPL-3.0">
    </a>
  </p>

  <p>
    <a href="https://arzolath.com">arzolath.com</a>
  </p>

</div>

---

## What is this?

**Instance Manager for VRChat** is a moderation-focused tool for managing VRChat instances.
Its purpose is simple: **automate actions against players you don’t want in your instance**, based on rules you define.

> **Not affiliated with VRChat Inc.** “VRChat” is a trademark of VRChat Inc.

---

## Status

Work in progress. Expect breaking changes while the core workflow and UI evolve.

---

## Features

- Rule-based handling of unwanted players
- Concepts like allowlists and blocklists
- Optional logging/audit trail of actions taken (planned)

---

## Download

Releases are published here:
- https://github.com/arzolath/InstanceManager-for-VRChat/releases

---

## Build from source

> Adjust paths/commands to match your actual solution once finalized.

### Requirements
- .NET SDK (latest LTS recommended)

### Build
```bash
dotnet restore
dotnet build -c Release
dotnet run -c Debug