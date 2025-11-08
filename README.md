# 🧠 Pommerman AI — Unity ML-Agents Project

Ten projekt to implementacja uproszczonej gry **Pommerman** w Unity z wykorzystaniem **ML-Agents** do trenowania agentów.  
Dwóch agentów rywalizuje na generowanej planszy, ucząc się zachowań ofensywnych i defensywnych.

---

## ⚙️ Wymagania

### 🔸 1. Oprogramowanie

| Komponent | Wersja (zalecana) | Uwagi |
|------------|------------------|--------|
| Unity | **2022.3 LTS** | Projekt testowany na 2022.3.x |
| Python | **3.10+** | Wymagany do ML-Agents |
| Conda / Miniconda | Dowolna aktualna wersja | Do izolacji środowiska |
| Visual Studio 2022 | Community / Professional	| Wymagane do kompilacji skryptów C# w Unity (zaznacz komponent Game development with Unity) |
| JetBrains Rider	(zamiennie dla Visual Studio) | 2024+ | Alternatywny IDE dla Unity, w pełni wspiera C# i integrację z Unity Editor |
| Unity Package: ML-Agents	| | Upewnij się, że jest zainstalowany w projekcie (com.unity.ml-agents) w Package Manager |

---

## 🧰 2. Instalacja środowiska Python (ML-Agents)

1. Upewnij się, że masz zainstalowaną [Anacondę lub Minicondę](https://docs.conda.io/en/latest/miniconda.html)
2. W katalogu projektu uruchom terminal i wpisz:

   ```bash
   conda env create -f environment.yml
   conda activate mlagents-env

    Sprawdź, czy ML-Agents zostało poprawnie zainstalowane:

    mlagents-learn --help

🧱 3. Uruchomienie projektu Unity

    Otwórz Unity Hub

    Wybierz: Add Project from Disk

    Wskaż folder z tym projektem (tam, gdzie znajduje się Assets/ i ProjectSettings/)

    Otwórz scenę Scenes/SampleScene.unity (lub odpowiednią, jeśli używasz innej)

🚀 4. Trenowanie agentów

    Upewnij się, że środowisko Conda pommerman-ai jest aktywne

    W terminalu wpisz:

    mlagents-learn pommerman_config.yaml --run-id=Pommerman_1v1_Final --force --time-scale=2.0 --no-graphics

    W Unity kliknij Play ▶️

    Trening rozpocznie się automatycznie — modele zapisywane będą w folderze results/

🔁 5. Resetowanie / generowanie planszy

Plansza jest generowana automatycznie przez ArenaManager.cs:

    Dwóch agentów startuje w przeciwległych rogach.

    Strefy startowe są wolne od przeszkód.

    Plansza losowo generuje ściany solidne i zniszczalne.
