## Disclaimer

This chess engine is not finished !


Currently only the classical evaluation function is working and usable. The NNUE functions have either bugs (which is probably the case for the HalfKav2 version) or no netfile with the requiered strength.

## Overview

Albatros is an open source UCI-compatible chess engine. It is not a complete chess programm and would require a UCI-compatible graphical user interface (GUI) (ex : Scid, Arena or Shredder) to be used comfortably.

The Albatros chess engine currently features 3 different evaluation functions for chess. The classical, which works with simple piece square tables (PSQT). And the two NNUE evaluation funtions, which are using the HalfKav2 and HalfKp architectures. The classical evaluation function is currently the only evaluation function that is implented efficiently.

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
   * searchmoves
   * ponder
   * mate
   * movestogo
 * ponderhit

### This is the list of all the available UCI options in Albatros:

 * #### Threads
   The number of CPU threads used for searching a position and for training. For best performance, set this equal to the number of CPU cores available.
   
 * #### Ponder 
   Let Albatros ponder its next move while the opponent is thinking (currently does not do anything).
   
 * #### EvalFile
   The name of the file containing the NNUE evaluation parameters.
   
 * #### EvalType
   The name of the neural network architecture you want to use. The two possibilities are Halfkp and HalfKav2.
   
 * #### Use NNUE
   Toggle between the NNUE and classical evaluation functions. If set to "true",
   the network parameters must be available to load from file (see also EvalFile).
   
 * #### c_puct
   Changes the exploration parameter of the puct tree.
   
For developers the following non-standard commands might be of interest, they are mainly useful for debugging and training:

  * #### d
    Display the current position, with ascii art and fen.
    
  * #### eval
    Return the evaluation of the current position.
    
  * #### export_net [filename]
    Exports the currently loaded network of the current network type to a file.
    
  * #### Training
    Starts to train the current NNUE net with the current training parameters.
    
Specifics about certain commands:

  * #### go depth 
    Start to search for the best move using the current evaluation function and an alpha beta pruned min max search (like stockfish).
    
  * #### go nodes 
    Start to search for the best move using the current evaluation function and a PUCT tree (like Leela chess zero), that uses the classical evaluation  function as a     predictor.  
