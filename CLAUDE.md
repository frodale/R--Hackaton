# RØ AS hackathon – AI Change Challenge

Hackathon workspace. We are a consulting team helping the fictional customer **RØ AS** with
**Operation FULL FART**. The task: "How can AI help RØ succeed with Operation FULL FART?"
Format: 75 minutes to build, then a 5-minute pitch/demo covering problem → solution → demo →
value → one key risk.

Jury weighting: customer value 30 %, smart use of AI 25 %, feasibility 20 %, innovation 15 %,
pitch 10 %. Principle: the best solution to the customer's problem wins, not the most advanced AI.

## Files

- `customer documents/` – source material from the organisers (Norwegian). Treat it as read-only.
  - `RO_AS_Operation_FULL_FART_Kundepakke.docx` – the main customer package: programme, milestones
    (P01–P12), 20 employee comments (C01–C20), steering group minutes (decisions B-04-xx, actions
    A-27–A-32), FAQ, emails (E-01–E-06), stakeholders, risk list (R01–R12), comms plan.
  - `RO_AS_fiktiv_case_norsk 1.docx` – company description and history.
  - `Rø struktur og utfordringer.pptx` – one slide per department with challenges and an agent idea,
    plus the "five questions every RØ agent must answer" checklist.
  - `RO_AS_IT-strategi.docx` – IT strategy 2027–2030 (Microsoft stack: M365, Dynamics 365,
    Power Platform/Copilot Studio, Azure/Fabric, AKS; reduce ServiceNow; FinOps; AI governance).
  - `Presentation.pptx` – ten personas built from the employee comments.
  - Other files: participant guide, task sheet (PDF), organiser's proposal, company deck.
- `RO_AS_Problem_Register.docx` – our deliverable: 27 problems sorted by impact, with description,
  type (documented / potential), score, evidence, who is affected and proposed AI solutions.

## Key facts about RØ AS (all fictional)

- ~2,500 employees in Norway, 8.4 bn NOK revenue, 8 business areas + 9 group functions (255 people).
- Core problem: after a global "rowing fever", demand exploded. The challenge is no longer creating
  demand but **delivering** it.
- Business areas and headcount: Marine 520, Motion 430, Labs 310, Helse 280, Digital 260,
  Prestasjon & Idrett 190, Opplevelser 170, Ventures 85.
- Highest risks: R01 promises exceed confirmed capacity (20), R02 carbon fibre / electronics
  shortage (20). The documents call the lack of one shared version of priorities, capacity and
  decisions "the risk behind the risks".

## Our assumptions (not in the documents – always label them as estimates)

- Revenue per business area (NOK bn): Motion 2.8, Marine 1.9, Digital 1.2, Helse 0.9,
  Prestasjon 0.65, Opplevelser 0.55, Ventures 0.25, Labs 0.15 (sums to 8.4).
- Personnel cost: 1.5 M NOK per employee per year (user's assumption) → 3.75 bn NOK total.
- Risk scores marked with * in the problem register are our own estimates.

## Current build: Material Allocation Assistant (C#)

The team chose to start with the **material allocation assistant** (shared problem of RØ Motion and
RØ Marine: carbon fibre, timber and electronics are scarce, R02). It is built first; the promise
checker below is the follow-up. Interfaces and storage are deliberately simple and will be refined.

- Solution: `RoAllocation.slnx` (.NET 11, Blazor Web App with interactive server rendering).
  - `src/RoAllocation.Core` – domain model (`Model.cs`), rule-based `AllocationEngine` (behind
    `IAllocationEngine`), and `InMemoryScenarioRepository` (behind `IScenarioRepository`) with mock data.
  - `src/RoAllocation.Web` – one dashboard page (`Components/Pages/Home.razor`), hand-written SVG chart
    components in `Components/Charts/`, formatting helpers in `Components/Fmt.cs`, styles in
    `wwwroot/ro.css` (light + dark via `prefers-color-scheme`). No JavaScript chart library.
  - `tests/RoAllocation.Core.Tests` – xUnit tests for the engine.
- Run: `dotnet run --project src/RoAllocation.Web --launch-profile http` → http://localhost:5049
- Test: `dotnet test`
- Strategies: **Max revenue** (best of several greedy orderings, guaranteed ≥ the other strategies –
  a test enforces this), **Critical customers first** (safety → elite deadline → strategic distributor →
  standard), **Fair share** (pro rata). Controls: carbon fibre supply cut (breaking-news button = 30 %)
  and a % of carbon fibre/timber reserved for Marine.
- Recommendation rule: fewest safety-critical gaps, then fewest elite gaps, then highest revenue. The
  page shows the trade-off in NOK versus Max revenue, and a person approves (B-04-02).
- **Forecast Dec 2026 – Dec 2027** (`Forecast.cs`, `ForecastEngine` behind `IForecastEngine`): runs the
  allocation month by month. The baseline scenario is one average month; `ForecastPlan` (from the
  repository) holds monthly demand factors per order line, a monthly supply plan per material,
  cancellation rates per customer tier and timeline events. Unfilled quantity carries over as backlog;
  each month part of it is cancelled (safety/elite 0 %, strategic 15 %, standard 30 %).
  Assumed patterns: rowing boom grows to summer 2027 then fades (C12), leisure season peaks Apr–Jul,
  national team orders before the championships (Apr–May), 17 May ceremonial peak; alternative carbon
  fibre supplier from Apr 2027 (+1,000 kg, P03/A-28), second electronics supplier from May (+500 kits),
  more carbon fibre from Aug (+500 kg).
- The page: forecast chart (delivered Motion/Marine, pushed to backlog, lost, new-orders line, numbered
  events), month-by-month heatmap (coverage per material, safety gaps, backlog), the three strategies
  compared over 13 months, then the selected month in detail (the original dashboard). Clicking a month
  in the chart or heatmap selects it.
- Forecast story with default settings: Max revenue NOK 569.8 M delivered, NOK 58.6 M lost, rescue
  equipment short in 9 of 13 months; Critical customers first NOK 511.9 M, NOK 120.9 M lost, safety
  covered all year. Backlog peaks in Jul 2027; shortages last until autumn even with the new suppliers.
- Mock data (prices, bills of material, orders, supply, forecast patterns) is our assumption, inspired
  by E-01/E-02.
- Screenshots for checking layout: headless Chrome works
  (`chrome.exe --headless=new --screenshot=... http://localhost:5049/`); headless Edge fails here.

## Next demo: the "promise checker"

Focus: RØ Motion and RØ Marine, problem cluster R01 / R02 / R06, decisions B-04-01 and B-04-02,
milestone P10 (red), emails E-01 (CRM says April, capacity sheet May, production June) and E-02.

Agreed approach (not yet built):
- **One self-contained HTML file**, plain JS + Chart.js from a pinned CDN version, mock data based
  on the case. No backend, no build step. Can be published as a shareable claude.ai artifact.
- A salesperson asks e.g. "Can we deliver 200 RØ One to Distributor Nord in June?" and gets a
  traffic light (green / yellow = partial + approval request / red = earliest realistic date),
  a capacity vs. committed-orders chart, and a plain-language "why".
- A "before" view showing the three conflicting dates, then the one confirmed date.
- A "breaking news" button that cuts carbon fibre supply by 30 % and updates all answers.
- The capacity check is rule-based (deterministic, explainable). AI is optional, only for reading
  free-text questions/emails and drafting the reply to the distributor.
- Pitch framing: in production this would be a Copilot Studio agent reading capacity data from
  Fabric, matching RØ's IT strategy. Human approves any exception (B-04-02).
- Open question: demo language English or Norwegian (case and jury are Norwegian).

## Environment notes

- Windows. Python is **not** installed; Node.js and the .NET SDK (8 and 11) are available.
- To read .docx/.pptx without Python: unzip and strip XML (PowerShell `System.IO.Compression`).
- Word documents are generated with the npm `docx` package (install it in a scratch folder, not in
  this repo). Microsoft Word is installed and can export to PDF via COM for checking layout.
- Git repo on branch `master`; nothing has been committed yet. Commit only when asked.

## Working style

- Answer in English unless asked otherwise; source documents are Norwegian.
- Always separate what is documented in the case from what is assumed, and cite the case IDs
  (C01, E-01, R01, B-04-02, …) as evidence.
- Keep the five agent questions from RØ's IT strategy in mind for any solution: who owns it,
  what problem it solves, what data it may use, where a human takes over, what it costs.
