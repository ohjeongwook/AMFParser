# AMFParser

AMFParser plugin for Fiddler2 web debugger. It can be used for parsing and displaying AMF data inside HTTP's POST requests and responses. To know more about AMF or Adobe Message Format, please refer to http://en.wikipedia.org/wiki/Action_Message_Format.

This project is to provide AMF binary POST data parser functionality to Fiddler2. It works as Fiddler2 addon and shows it's own tab inside Fiddler2 inspector tab. This addon can be very helpful when you need to parse and display what's inside AMF data.

Currently it provides parsing functionality and experimental editing functionality.

Old repository is left for reference purpose only: https://amfparser.codeplex.com/

## System Requirements
### FOR USE

  * Fiddler2
  * .Net Framework 2 or greater

### FOR DEVELOPMENT
Visual Studio 2003 or greater

## How to Install

Copy AMFParser.dll to Inspectors directory under Fiddler2 installation directory(usually %ProgramFiles%\Fiddler2\Inspectors).

## How to use

Launch Fiddler2
The AMF tab will appear as one of Fiddler2 inspector tabs.
The AMF tab will show data whenever it encounters AMF encoded POST data.

## New BSD License (BSD)
Copyright (c) 2009, Jeongwook Oh
All rights reserved.

Redistribution and use in source and binary forms, with or without modification, are permitted provided that the following conditions are met:

* Redistributions of source code must retain the above copyright notice, this list of conditions and the following disclaimer.

* Redistributions in binary form must reproduce the above copyright notice, this list of conditions and the following disclaimer in the documentation and/or other materials provided with the distribution.

* Neither the name of Bugtruck nor the names of its contributors may be used to endorse or promote products derived from this software without specific prior written permission.

THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS "AS IS" AND ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE IMPLIED WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE DISCLAIMED. IN NO EVENT SHALL THE COPYRIGHT OWNER OR CONTRIBUTORS BE LIABLE FOR ANY DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES (INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES; LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND ON ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT (INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
