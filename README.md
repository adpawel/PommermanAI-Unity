# 🧠 Pommerman AI — Unity ML-Agents

Celem projektu było zaprojektowanie, implementacja oraz eksperymentalna analiza środowiska gry **Pommerman**, w którym autonomiczni agenci uczą się skutecznych strategii działania z wykorzystaniem metod uczenia ze wzmocnieniem (Reinforcement Learning). 

## Zakres

- zaprojektowanie środowiska gry inspirowanego klasyczną mechaniką Bombermana, obejmującego generowanie planszy, obsługę przeszkód stałych i zniszczalnych, mechanikę ładunków oraz system kolizji,
- implementację agentów uczących się z wykorzystaniem algorytmu Proximal Policy Optimization (PPO), w tym definiowanie przestrzeni obserwacji, przestrzeni akcji oraz funkcji nagrody,
- stworzenie środowiska wieloagentowego umożliwiającego rywalizację agentów w trybach 1v1 oraz 2v2, z uwzględnieniem mechanizmów self-play,
- analizę wpływu liczby obserwacji, struktury stanu oraz funkcji nagrody na stabilność i jakość procesu uczenia,
- implementację mechanizmów umożliwiających „zamrażanie” wytrenowanych agentów oraz trenowanie nowych agentów w ich otoczeniu,
- eksport wytrenowanych modeli do postaci umożliwiającej ich wykorzystanie w gotowej aplikacji (pliki .onnx) oraz przygotowanie wersji wykonywalnej programu

---
## Najważniejsze elementy

W początkowej fazie przeprowadzono eksperymentalne porównanie algorytmów A2C (Advantage Actor-Critic) oraz PPO (Proximal Policy Optimization). Ze względu na większą stabilność, lepszą konwergencję i mniejszą wariancję nagród wybrano PPO jako główny algorytm treningowy.

Proces uczenia był stopniowo rozwijany poprzez:
- **Reward Shaping** – projektowanie gęstej funkcji nagrody wspierającej eksplorację, unikanie zagrożeń i zachowania ofensywne
- **Curriculum Learning** – etapowe zwiększanie złożoności środowiska (od eksploracji po pełną rywalizację)
- **Imitation Learning / Behavioral Cloning** – wykorzystanie demonstracji eksperckich do przełamania strategii zbyt defensywnych
- **Transfer Learning** – ponowne użycie wytrenowanych modeli jako punktu startowego dla bardziej złożonych wariantów
  
Self-Play – trening poprzez rywalizację z zamrożonymi lub wcześniejszymi wersjami agenta

---

## Demo
https://github.com/user-attachments/assets/a86497cb-acd7-498b-9b89-7570371cee7f


## Wymagania

### 1. Oprogramowanie

| Komponent | Wersja (zalecana) | Uwagi |
|------------|------------------|--------|
| Unity | **2022.3 LTS** | Projekt testowany na 2022.3.x |
| Python | **3.10+** | Wymagany do ML-Agents |
| Conda / Miniconda | Dowolna aktualna wersja | Do izolacji środowiska |
| Visual Studio 2022 | Community / Professional	| Wymagane do kompilacji skryptów C# w Unity (zaznacz komponent Game development with Unity) |
| Unity Package: ML-Agents	| | Upewnij się, że jest zainstalowany w projekcie (com.unity.ml-agents) w Package Manager |

---

### 2. Instalacja środowiska Python (ML-Agents)

1. Upewnij się, że masz zainstalowaną [Anacondę lub Minicondę](https://docs.conda.io/en/latest/miniconda.html)
2. W katalogu projektu uruchom terminal i wpisz:

   ```bash
   conda env create -f environment.yml
   conda activate mlagents-env

    Sprawdź, czy ML-Agents zostało poprawnie zainstalowane:

    mlagents-learn --help

 3. Uruchomienie projektu Unity
    
    - Otwórz Unity Hub
    - Wybierz: Add Project from Disk
    - Wskaż folder z tym projektem (tam, gdzie znajduje się Assets/ i ProjectSettings/)
    - Otwórz scenę Scenes/SampleScene.unity

 4. Trenowanie agentów

    - Upewnij się, że środowisko Conda mlagents-env jest aktywne
    - W terminalu wpisz: `mlagents-learn pommerman_config.yaml --run-id=Pommerman_1v1_Final --force --time-scale=2.0`
    - W Unity kliknij Play ▶️
    - Trening rozpocznie się automatycznie — modele zapisywane będą w folderze `results/`

    Plansza losowo generuje ściany solidne i zniszczalne.

