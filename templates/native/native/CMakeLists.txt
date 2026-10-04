cmake_minimum_required(VERSION 3.16)
project(ModNameNative CXX)
add_library(ModNameNative SHARED mod.cpp)
set_target_properties(ModNameNative PROPERTIES PREFIX "" CXX_STANDARD 17 CXX_STANDARD_REQUIRED ON)
