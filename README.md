## Graphics Experiment - FFT Ocean Water Simulation

NOTE: needs at least Unity3D 5.3.4f1 to work due to a bugfix in that version.

Inspired by https://github.com/blackencino/EncinoWaves

Original paper is :

> Christopher J. Horvath. 2015.   
> [Empirical directional wave spectra for computer graphics.](http://dl.acm.org/authorize?N90195)   
> In Proceedings of the 2015 Symposium on Digital Production (DigiPro '15),   
> Los Angeles, Aug. 8, 2015, pp. 29-39. 

## Features

* Compute shader based FFT with configurable size (default is 512)
* Tessellated close ocean mesh locked to camera
* Ocean extended to horizon using dynamic frustum/plane intersection

## Disclaimer

As always, this can probably be done in a better way.
Please add issues if you have any suggestions.

## Results

[EncinoWaves Demo](https://i.imgur.com/6wiSy7v.gif)

### License 

Copyright &copy; 2016 Remi Gillig

Licensed under the Apache License, Version 2.0 (the "License");
you may not use this file except in compliance with the License.
You may obtain a copy of the License at

http://www.apache.org/licenses/LICENSE-2.0

Unless required by applicable law or agreed to in writing, software
distributed under the License is distributed on an "AS IS" BASIS,
WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
See the License for the specific language governing permissions and
limitations under the License.
