#include "mainwindow.h"
#include "ui_mainwindow.h"
#include <windows.h>

MainWindow::MainWindow(QWidget *parent) :
    QMainWindow(parent),
    ui(new Ui::MainWindow)
{
    ui->setupUi(this);
    init();
}

MainWindow::~MainWindow()
{
    delete ui;
}

/**
 * @brief MainWindow::init
 * Initialise the application at launch
 */
void MainWindow::init()
{
    //init the widget
    //init the file tree
    model = new QFileSystemModel;
    model->setRootPath(QDir::currentPath());
    ui->fileList->setModel(model);
    selectionModel = new QItemSelectionModel(model);
    QObject::connect(selectionModel, SIGNAL(selectionChanged(const QItemSelection &, const QItemSelection &)),
                     SLOT(on_file_selected(const QItemSelection & , const QItemSelection & )));
    ui->fileList->setSelectionModel(selectionModel);
    //init the drive list
    MainWindow::initDriveList();
}

/**
 * @brief MainWindow::initDriveList
 * initialise the list of drive
 */
void MainWindow::initDriveList()
{
    //get the list of drive
    DWORD drive = GetLogicalDrives();
    char szDrive[] = "A:";
    while(drive)
    {// use the bitwise AND, 1–available, 0-not available
        if(drive & 1)
        {
            driveList.append(QString(szDrive)+ QString("\\"));
        }
        // increment...
        ++szDrive[0];
         // shift the bitmask binary right
         drive >>= 1;
    }
    //add the list
    for(int i = 0; i <driveList.length();i++)
    {
        ui->driveList->addItem(driveList.at(i));
        qDebug() << driveList.at(i);
    }
    //add an action on the list

}

/**
 * @brief MainWindow::initSelectedDrive
 * Initialise the tree with the list of file
 * @param selectedDrive the selected drive
 */
void MainWindow::initSelectedDrive(const QString selectedDrive)
{
    //set the drive of the tree
     ui->fileList->setRootIndex(model->index(selectedDrive));
}

void MainWindow::on_driveList_currentTextChanged(const QString &arg1)
{
    initSelectedDrive(arg1);
}

void MainWindow::on_file_selected(const QItemSelection & selected, const QItemSelection & deselected)
{
    qDebug() << model->data(selected.indexes().at(0)).value<QString>();
    //check if the file is a .NEF file
    QString file = model->data(selected.indexes().at(0)).value<QString>();
    if(file.split(".").last().toLower() == "nef")
    {
        qDebug() << "A NEF file " << file;
        //call the display function
    }else{
        qDebug() << "Not a NEF file";
    }
}
