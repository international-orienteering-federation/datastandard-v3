# Example files

[Here](Event_name_and_start_time.xml) is an example of an XML file that conforms to the IOF Interface Standard version 3.0. It contains the name and start time for an orienteering event.

A few things to note:

* To avoid confusion, support multilingualism and maximize compatibility, it is strongly recommended that the UTF-8 character encoding is used, although other encodings are allowed. Please express the encoding explicitly in the XML declaration: ```<?xml version="1.0" encoding="…"?>```
* XML files must be saved in UTF-8 encoding and also express it explicitly in the XML declaration.
* The namespace ```https://www.orienteering.org/datastandard/3.0``` is used. The namespace is preferably declared as the default namespace in the root element. Additional namespaces can be defined here as well, allowing for extensions.
* The ```iofVersion``` attribute defines the version used, and must be set to 3.0.
* The ```createTime``` attribute defines the time when the file was created.
* The ```creator``` attribute defines the name of the software that created the file.