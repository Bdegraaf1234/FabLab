# FabLab
Integrated LC-MS/MS-based de novo sequencing of antibody chains in complex mixtures

## Getting started
Download and run the latest installer (```fablab_installer.exe```) from the [releases page](https://github.com/Bdegraaf1234/FabLabPublic/releases). FabLab is only compatible with windows as the graphical user interface (GUI) was built using Windows Forms.

## Building/Developing
To build FabLab from source, download the source code for FabLab using git clone.

```
git clone https://github.com/Bdegraaf1234/FabLabPublic.git
```

The project is built using dotnet (.NET Framework 4.8) and development is done on Windows using [visual studio](https://visualstudio.microsoft.com/downloads/). To build the project, install [dotnet](https://dotnet.microsoft.com/download) and enter the following commands into your terminal from the root folder of this repository, ```FabLabPublic/```.

```
dotnet restore /p:configuration=release
dotnet build /p:configuration=release
```

This will generate an executable, ```FabLab.exe```, in the build folder (default: ```./FabLab/bin/release/net4.8/```). The executable needs the other files in this folder to run so it is advised to either run the software from the build folder or generate an installer using for example [inno-setup](https://jrsoftware.org/isdl.php).

## Examples
In the ```./examples``` folder you can find several example datasets. If you have installed the software, you can click any of the ```.flconfig``` files and the software will start after preprocessing the data (this may up to 1 minute). If you have built the software yourself you can start it by running the ```FabLab.exe``` file. You can then click ```File --> Load --> From config file``` and navigate to one of the .flconfig files. You are encouraged to play around with these datasets before you move on to your own data, as the software is relatively unstable and requires some experience to operate. For a detailed elaboration on the testing data and underlying algorithms, you are referred to the publication in ./CITATION.cff.

Examples are organized per target chain, and include 1-3 .flconfig files. A good start would be the ```polyclonal_IgA1 example``` and opening the .flconfig files in the following order while following along with the selection process by exploring the plots and scores:

```
Examples/polyclonal_IgA1/1.framework_region_sequencing.flconfig
Examples/polyclonal_IgA1/2.cdr_sequencing.flconfig
Examples/polyclonal_IgA1/3.chain_sequencing.flconfig
```

The example datasets work fine with the default settings. Navigating the user interface is done with the mouse. Tables, column headers and rows can be right-clicked to reveal contextual options. On the left side of the main screen you can find several settings that you can change. 

## Analyzing your own data
All tables can be filtered and sorted by right-clicking the column headers. Filtered segment candidates should be selected for in depth scoring/inspection in a separate view (rightclick, ```Analyze --> Rescore all shown candidates for this region```) and exported (rightclick, ```Write --> ...```) for subsequent stages. Broadly, you should aim to reject incorrect FR candidates using the scores, graphs and annotated spectra. You can select framework region (FR) candidates for further analysis (```Select for prediction``` to analyze directly, or ```Check contig for recombination``` for automated analysis of multiple combinations.
When you have selected one or more adjacent FR pairs (e.g. one FR1 and three FR2) then you can recombine the into FR-CDR-FR candidates. This is initially done by rightclicking a CDR (Complementarity Determining Region) table, and clicking ```Analyze --> Recombine adjacent checked candidates```. A new tab with scored FR-CDR-FR candidates will then open. Export the best of these candidates and create a new ```.flconfig``` file to move on to the next stage (or in the case of the examples, just open the next ```.flconfig``` file).

### Preprocessing
#### de novo peptides
De novo peptides for analysis were generated from shotgun LC-MS/MS data using [PEAKS](https://www.bioinfor.com/peaks-ab-software/) de novo sequencing software. To support other sources of de novo peptides, open an issue or contact the authors. These peptides were analyzed using the [STITCH](https://github.com/snijderlab/stitch/) Bottom-Up sequencing tool to select a germline template from the [IMGT](https://www.imgt.org/vquest/refseqh.html) database and generate a starting pool of FR candidates ([proforma format](https://www.psidev.info/proforma)).
#### Middle-Down fragmentation data
Middle down data was acquired as described in the publication in ./CITATION.cff. These data were deconvoluted using [FreeStyle](https://www.thermofisher.com/nl/en/home/technical-resources/technical-reference-library/mass-spectrometry-support-center/liquid-chromatography-mass-spectrometry-software-support/freestyle-software-support.html) and [BioPharmaFinder](https://www.thermofisher.com/nl/en/home/technical-resources/technical-reference-library/mass-spectrometry-support-center/liquid-chromatography-mass-spectrometry-software-support/biopharma-finder-software-support.html). A potential (Free) alternative is [ms_deisotope](https://github.com/mobiusklein/ms_deisotope). Deconvoluted Middle down spectra should be supplied in mgf format, where all fragment ions have been converted to monoisotopic singly charged masses. The precursor mass is given as an average, singly charged mass.

# Credits
### Developers
* Bastiaan de Graaf - Software engineer - s.c.degraaf{at}uu{dot}nl
* Douwe Schulte - collaborating developer, lead developer for the [Stitch](https://github.com/snijderlab/stitch/) Bottom-Up sequencing tool
### Academic guidance
* Albert Heck - Principal investigator
* Richard Scheltema - Developer of the hecklib dependencies
### Eperimental support
* Sem Tamara - Experimental method development
* Max Hoek - Experimental method development
* Albert Bondt - Experimental method development
* Weiwei Peng - Experimental method development

## Acknowledgements
* All credited individuals are part of the group ["Biomolecular Mass Spectrometry and Proteomics"](https://www.uu.nl/en/research/biomolecular-mass-spectrometry-and-proteomics) ([or here](https://www.hecklab.com/biomolecular-mass-spectrometry-and-proteomics/)) at the [Utrecht University](https://www.uu.nl/)

## Dependencies
- FabLab is built on the Hecklib MS Data analysis packages. These packages are precompiled into a single nuget dependency for direct consumption, located in the ```nuget/``` folder.

## License
GPLV2 License (see LICENSE.md)
