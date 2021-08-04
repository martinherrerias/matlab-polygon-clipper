# matlab-polygon-clipper
An unofficial fork of Erik Johnson's MEX interface to Angus Johnsons's polygon clipper.

BRANCHES:
   main -- contains Erik Johnson's code with minor fixes
   polygon -- adapted to work with the polygon class in https://github.com/martinherrerias/matlab-utils

FUNCTIONS:
   polyclip -- find the difference, intersection, xor or union of two polygons
   polyout  -- outset (or inset) a polygon's vertices

The work is done by the MEX file matlab/clipper (which must be compiled).

INSTALLATION

   Make '.../clipper/matlab' the working directory, and compile for your platform:

      mex '-D__int64=__int64_t' '-I../cpp' '../cpp/clipper.cpp' 'mexclipper.cpp'

   Run polyclip_test and polyout_test to check that everything works allright.

CREDITS

   CPP code updated directly from Angus Johnson's:
      http://www.angusj.com/delphi/clipper.php
      https://sourceforge.net/p/polyclipping/code/HEAD/tree/trunk/

   Original MEX wrapper by Prof. Erik A Johnson <JohnsonE@usc.edu>,
      https://de.mathworks.com/matlabcentral/fileexchange/61329-new-polygon-clipping-and-offsetting

   ... itself a re-write of one originally written by Emmett
      http://www.mathworks.com/matlabcentral/fileexchange/36241-polygon-clipping-and-offsetting

   ... which was based on Sebastian Holz's Polygon Clipper mex wrapper for the GPC library
      https://www.mathworks.com/matlabcentral/fileexchange/8818-polygon-clipper

See original licese files in ./matlab and ./cpp
