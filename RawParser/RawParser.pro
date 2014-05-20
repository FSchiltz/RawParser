#-------------------------------------------------
#
# Project created by QtCreator 2014-05-20T09:25:54
#
#-------------------------------------------------

QT       += core gui

greaterThan(QT_MAJOR_VERSION, 4): QT += widgets

TARGET = RawParser
TEMPLATE = app


SOURCES += main.cpp\
        mainwindow.cpp \
    rawreader.cpp \
    rawdemos.cpp \
    rawdisplay.cpp \
    imagewriter.cpp \
    rawimage.cpp

HEADERS  += mainwindow.h \
    rawreader.h \
    rawdemos.h \
    rawdisplay.h \
    imagewriter.h \
    rawimage.h

FORMS    += mainwindow.ui
