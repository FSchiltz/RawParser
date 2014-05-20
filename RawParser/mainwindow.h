#ifndef MAINWINDOW_H
#define MAINWINDOW_H

#include <QMainWindow>
#include <QFileSystemModel>
#include <QDebug>
#include <QItemSelectionModel>


using namespace std;


namespace Ui {
class MainWindow;
}

class MainWindow : public QMainWindow
{
    Q_OBJECT

public:
    explicit MainWindow(QWidget *parent = 0);
    ~MainWindow();

private slots:
    void on_driveList_currentTextChanged(const QString &arg1);
    void on_file_selected(const QItemSelection & selected, const QItemSelection & deselected);

private:
    Ui::MainWindow *ui;
    QList<QString> driveList;
    QString selecteDrive;
    QString selectedFile;
    QFileSystemModel *model;
    QItemSelectionModel *selectionModel;
    void init();
    void initDriveList();
    void initSelectedDrive(const QString selectedDrive);
};
#endif // MAINWINDOW_H
