# MiniTube Test Suite — 13 tests

## The Big Picture

```mermaid
graph LR
    subgraph UNIT["UNIT TESTS (8)"]
        direction TB
        A["Call a method directly"]
        B["Test ONE class in isolation"]
        C["No HTTP, no middleware, no routing"]
    end

    subgraph INTEGRATION["INTEGRATION TESTS (5)"]
        direction TB
        D["Send a real HTTP request"]
        E["Test the WHOLE app wired together"]
        F["Auth + routing + pages + DB all running"]
    end

    UNIT ---|"test pieces"| CODE["Your Code"]
    INTEGRATION ---|"test the machine"| CODE

    style UNIT fill:#1a3a1a,stroke:#4afa4a,color:#fff
    style INTEGRATION fill:#3a1a1a,stroke:#fa4a4a,color:#fff
    style CODE fill:#1a1a2a,stroke:#aaa,color:#fff
```

---

## Unit Tests: Two Techniques

Both are unit tests. The difference is how they fake the dependency.

```mermaid
graph TB
    subgraph REAL["Technique 1: InMemory DB (VideoServiceTests)"]
        direction LR
        T1["Your test"] -->|"inserts real rows"| DB["InMemory DB<br/>────────────<br/>Music video<br/>Tech video<br/>Gaming video"]
        DB -->|"real LINQ queries<br/>filter, sort, score"| VS["VideoService<br/>.GetRelatedVideosAsync()"]
        VS -->|"returns real results"| T1
    end

    subgraph FAKE["Technique 2: Moq (AdminClaimsTransformationTests)"]
        direction LR
        T2["Your test"] -->|"scripts one answer"| MOQ["Mock IConfiguration<br/>────────────<br/>AdminEmail?<br/>→ admin@x.com<br/><br/>Anything else?<br/>→ I don't know"]
        MOQ -->|"returns canned value"| ACT["AdminClaimsTransformation<br/>.TransformAsync()"]
        ACT -->|"returns claim result"| T2
    end

    style REAL fill:#1a3a1a,stroke:#4afa4a,color:#fff
    style FAKE fill:#1a1a3a,stroke:#4a4afa,color:#fff
```

### When to pick which?

```mermaid
graph TD
    Q{"Does the dependency<br/>have complex behavior<br/>your code relies on?"}
    Q -->|"YES<br/>queries, filters, joins,<br/>saves state"| INMEMORY["Use InMemory DB<br/>────────────<br/>Let real logic run<br/>against real data"]
    Q -->|"NO<br/>just returns a value<br/>or checks a flag"| MOQ["Use Moq<br/>────────────<br/>Script the answer<br/>in one line"]

    style Q fill:#2a2a2a,stroke:#ffa,color:#fff
    style INMEMORY fill:#1a3a1a,stroke:#4afa4a,color:#fff
    style MOQ fill:#1a1a3a,stroke:#4a4afa,color:#fff
```

---

## How Each Test Type Talks to the Code

```mermaid
sequenceDiagram
    box rgb(26, 58, 26) Unit Test (InMemory DB)
    participant UT as VideoServiceTests
    end
    participant VS as VideoService
    participant DB as InMemory DB

    UT->>DB: Insert 4 videos (Music, Tech, etc.)
    UT->>VS: GetRelatedVideosAsync("target-id")
    VS->>DB: SELECT WHERE Id != target ORDER BY score
    DB-->>VS: [Music video, Tech video]
    VS-->>UT: List of related videos
    UT->>UT: Assert: Music video is first
```

```mermaid
sequenceDiagram
    box rgb(26, 26, 58) Unit Test (Moq)
    participant MT as AdminClaimsTests
    end
    participant ACT as AdminClaimsTransformation
    participant MOQ as Mock IConfiguration

    Note over MT,MOQ: Setup: mock["AdminEmail"] returns "admin@x.com"
    MT->>ACT: TransformAsync(user with email "admin@x.com")
    ACT->>MOQ: What is config["AdminEmail"]?
    MOQ-->>ACT: "admin@x.com"
    ACT->>ACT: Emails match! Add IsAdmin claim
    ACT-->>MT: ClaimsPrincipal with IsAdmin=true
    MT->>MT: Assert: HasClaim("IsAdmin", "true")
```

```mermaid
sequenceDiagram
    box rgb(58, 26, 26) Integration Test
    participant IT as IntegrationTests
    end
    participant HTTP as HttpClient
    participant MW as Auth Middleware
    participant RZ as Razor Page /Upload

    IT->>IT: Set TestAuthHandler.IsAuthenticated = false
    IT->>HTTP: GET /Upload
    HTTP->>MW: Request arrives
    MW->>MW: Check auth: user is anonymous
    MW->>MW: Page has [Authorize]
    MW-->>HTTP: 401 Unauthorized
    HTTP-->>IT: Response: status 401
    IT->>IT: Assert: user was blocked
```

---

## All 13 Tests at a Glance

### Unit Tests with InMemory DB (3) — VideoServiceTests.cs

Tests `VideoService.GetRelatedVideosAsync()` — the algorithm that picks which videos appear in the "Related Videos" sidebar.

```
┌─────────────────────────────────────────────────────────────────┐
│  TEST                          │  WHAT GOES IN    │  EXPECTED   │
├─────────────────────────────────────────────────────────────────┤
│  PrefersSameCategory           │  1 Music target   │  Music      │
│                                │  1 Music other    │  ranks      │
│                                │  2 other genres   │  first      │
├─────────────────────────────────────────────────────────────────┤
│  ExcludesCurrentVideo          │  target + 2 others│  target     │
│                                │                   │  NOT in     │
│                                │                   │  results    │
├─────────────────────────────────────────────────────────────────┤
│  ScoresSharedKeywordsHigher    │  "Razor Pages"    │  "Razor     │
│                                │  "Razor tutorial"  │  tutorial"  │
│                                │  "Cooking pasta"   │  ranks      │
│                                │                   │  above      │
│                                │                   │  "Cooking"  │
└─────────────────────────────────────────────────────────────────┘
```

### Unit Tests with Moq (5) — AdminClaimsTransformationTests.cs

Tests `AdminClaimsTransformation.TransformAsync()` — the logic that decides who gets admin privileges.

```
┌──────────────────────────────────────────────────────────────────────┐
│  TEST                          │  MOCK RETURNS     │  USER EMAIL    │  RESULT    │
├──────────────────────────────────────────────────────────────────────┤
│  EmailMatches                  │  admin@x.com      │  admin@x.com   │  IsAdmin   │
├──────────────────────────────────────────────────────────────────────┤
│  EmailDoesNotMatch             │  admin@x.com      │  random@x.com  │  No claim  │
├──────────────────────────────────────────────────────────────────────┤
│  NotAuthenticated              │  admin@x.com      │  (anonymous)   │  No claim  │
├──────────────────────────────────────────────────────────────────────┤
│  CaseInsensitive               │  admin@x.com      │  ADMIN@X.COM   │  IsAdmin   │
├──────────────────────────────────────────────────────────────────────┤
│  ConfigMissing                 │  null             │  anyone@x.com  │  No claim  │
└──────────────────────────────────────────────────────────────────────┘
```

### Integration Tests (5) — IntegrationTests.cs

Boots the full app and sends real HTTP requests.

```
┌──────────────────────────────────────────────────────────────────────┐
│  TEST                          │  REQUEST          │  USER STATE    │  EXPECTED  │
├──────────────────────────────────────────────────────────────────────┤
│  Homepage_Returns200           │  GET /             │  anonymous     │  200 OK    │
├──────────────────────────────────────────────────────────────────────┤
│  Upload_AnonymousIsBlocked     │  GET /Upload       │  anonymous     │  blocked   │
├──────────────────────────────────────────────────────────────────────┤
│  Edit_AnonymousIsBlocked       │  GET /Edit?id=x    │  anonymous     │  blocked   │
├──────────────────────────────────────────────────────────────────────┤
│  Upload_AuthenticatedReturns200│  GET /Upload       │  logged in     │  200 OK    │
├──────────────────────────────────────────────────────────────────────┤
│  Upload_ExeFileIsRejected      │  POST /Upload      │  logged in     │  rejected  │
│                                │  (malware.exe)     │                │            │
└──────────────────────────────────────────────────────────────────────┘
```

---

## What Covers What

```mermaid
graph TB
    subgraph APP["MiniTube App"]
        VS["VideoService<br/>──────────────<br/>GetRelatedVideosAsync ← tested<br/>GetAllAsync ← NOT tested<br/>SaveVideoAsync ← NOT tested<br/>ToggleLikeAsync ← NOT tested<br/>AddCommentAsync ← NOT tested<br/>CanUserEditAsync ← NOT tested<br/>...15+ more ← NOT tested"]

        ACT["AdminClaimsTransformation<br/>──────────────<br/>TransformAsync ← tested"]

        PAGES["Pages + Middleware<br/>──────────────<br/>GET / ← tested<br/>GET /Upload ← tested<br/>GET /Edit ← tested<br/>POST /Upload ← tested<br/>GET /Watch ← NOT tested<br/>POST like/dislike ← NOT tested<br/>POST comment ← NOT tested"]
    end

    UT["Unit Tests (3)"] -.->|InMemory DB| VS
    MOCK["Unit Tests with Moq (5)"] -.->|Mock| ACT
    INT["Integration Tests (5)"] -.->|HTTP requests| PAGES

    style UT fill:#1a3a1a,stroke:#4afa4a,color:#fff
    style MOCK fill:#1a1a3a,stroke:#4a4afa,color:#fff
    style INT fill:#3a1a1a,stroke:#fa4a4a,color:#fff
    style APP fill:#1a1a1a,stroke:#888,color:#fff
```

---

## How to Run

```bash
# All 13 tests
dotnet test MiniTube.Tests/MiniTube.Tests.csproj --configuration Release

# Only unit tests (InMemory DB)
dotnet test MiniTube.Tests/MiniTube.Tests.csproj --filter "FullyQualifiedName~VideoServiceTests"

# Only unit tests (Moq)
dotnet test MiniTube.Tests/MiniTube.Tests.csproj --filter "FullyQualifiedName~AdminClaimsTransformation"

# Only integration tests
dotnet test MiniTube.Tests/MiniTube.Tests.csproj --filter "FullyQualifiedName~IntegrationTests"
```
