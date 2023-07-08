## Overview

Albatros is an open source UCI-compatible chess engine. It is not a complete chess programm and would require a UCI-compatible graphical user interface (GUI) (ex : Scid, Arena or Shredder) to be used comfortably.

The Albatros chess engine currently features 2 different evaluation functions for chess. The classical, which works with simple piece square tables (PSQT from the chess engine [RofChade](https://rofchade.nl/)). And the NNUE evaluation funtion, which has the format 768 -> 256x2 -> 1. The NNUE is trained on lichess games. It needs avx2 to work.

## Play against Albatros

Albatros is hosted on lichess [here](https://lichess.org/@/Albatros_Engine) and runs on my Raspberry Pi.

## Files

This Distribution of Albatros contains the following files:

* [Readme.md](https://github.com/PiIsRational/Albatros/blob/master/README.md), the File you are currently reading.
* [Albatros Engine](https://github.com/PiIsRational/Albatros/tree/master/Albatros%20Engine), a subdirectory that contains the whole sourcecode.
* [Albatros Engine.sln](https://github.com/PiIsRational/Albatros/blob/master/Albatros%20Engine.sln), the solution file of the programm.
* [License.txt](https://github.com/PiIsRational/Albatros/blob/master/LICENSE), a text file that contains the GNU general public license version 2.
* There sould be a file with a .nnue extention, but there are currently no such files with a required strength.

## The UCI protocol and available options

The Universal Chess Interface (UCI) is a standard protocol used to communicate with a chess engine, and is the recommended way to do so for typical graphical user interfaces (GUI) or chess tools. Albatros does currently not support the following options, as they are describerd in the [UCI protocoll](https://www.shredderchess.com/download/div/uci.zip):

 * go
   * nodes
   * searchmoves
   * ponder
   * mate
 * ponderhit

### This is the list of all the available UCI options in Albatros:

 * #### EvalFile
   The name of the file containing the NNUE evaluation parameters.

 * #### Use NNUE
   Toggle between the NNUE and classical evaluation functions. If set to "true",
   the network parameters must be available to load from file (see also EvalFile).
   
 * #### Hash
   The size of the Transposition Table in Mb
   
For developers the following non-standard commands might be of interest, they are mainly useful for debugging and training:

  * #### d
    Display the current position, with ascii art and fen.
    
  * #### eval
    Return the evaluation of the current position.
    
  * #### export_net [filename]
    Exports the currently loaded network of the current network type to a file.
    
  * #### Training
    Starts to train the current NNUE net with the current training parameters.
