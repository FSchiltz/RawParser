/********************************************************************************
** Form generated from reading UI file 'mainwindow.ui'
**
** Created by: Qt User Interface Compiler version 5.3.0
**
** WARNING! All changes made in this file will be lost when recompiling UI file!
********************************************************************************/

#ifndef UI_MAINWINDOW_H
#define UI_MAINWINDOW_H

#include <QtCore/QVariant>
#include <QtWidgets/QAction>
#include <QtWidgets/QApplication>
#include <QtWidgets/QButtonGroup>
#include <QtWidgets/QComboBox>
#include <QtWidgets/QGraphicsView>
#include <QtWidgets/QGridLayout>
#include <QtWidgets/QGroupBox>
#include <QtWidgets/QHeaderView>
#include <QtWidgets/QLabel>
#include <QtWidgets/QMainWindow>
#include <QtWidgets/QPushButton>
#include <QtWidgets/QSlider>
#include <QtWidgets/QTreeView>
#include <QtWidgets/QWidget>

QT_BEGIN_NAMESPACE

class Ui_MainWindow
{
public:
    QWidget *centralWidget;
    QGridLayout *gridLayout;
    QGroupBox *horizontalGroupBox;
    QGridLayout *gridLayout_2;
    QGroupBox *FilePanel;
    QGridLayout *gridLayout_4;
    QComboBox *driveList;
    QTreeView *fileList;
    QGroupBox *DisplayPanel;
    QGridLayout *gridLayout_3;
    QGraphicsView *graphicsView;
    QGroupBox *ToolsPanel;
    QGridLayout *gridLayout_5;
    QPushButton *pushButton;
    QGroupBox *groupBox;
    QGridLayout *gridLayout_6;
    QSlider *horizontalSlider;
    QLabel *label;
    QLabel *label_2;
    QSlider *horizontalSlider_2;

    void setupUi(QMainWindow *MainWindow)
    {
        if (MainWindow->objectName().isEmpty())
            MainWindow->setObjectName(QStringLiteral("MainWindow"));
        MainWindow->resize(700, 500);
        MainWindow->setMinimumSize(QSize(700, 500));
        centralWidget = new QWidget(MainWindow);
        centralWidget->setObjectName(QStringLiteral("centralWidget"));
        centralWidget->setMinimumSize(QSize(700, 500));
        gridLayout = new QGridLayout(centralWidget);
        gridLayout->setSpacing(6);
        gridLayout->setContentsMargins(11, 11, 11, 11);
        gridLayout->setObjectName(QStringLiteral("gridLayout"));
        horizontalGroupBox = new QGroupBox(centralWidget);
        horizontalGroupBox->setObjectName(QStringLiteral("horizontalGroupBox"));
        gridLayout_2 = new QGridLayout(horizontalGroupBox);
        gridLayout_2->setSpacing(6);
        gridLayout_2->setContentsMargins(11, 11, 11, 11);
        gridLayout_2->setObjectName(QStringLiteral("gridLayout_2"));
        gridLayout_2->setSizeConstraint(QLayout::SetMinAndMaxSize);
        gridLayout_2->setContentsMargins(1, 1, 1, 1);
        FilePanel = new QGroupBox(horizontalGroupBox);
        FilePanel->setObjectName(QStringLiteral("FilePanel"));
        FilePanel->setMinimumSize(QSize(100, 0));
        gridLayout_4 = new QGridLayout(FilePanel);
        gridLayout_4->setSpacing(6);
        gridLayout_4->setContentsMargins(11, 11, 11, 11);
        gridLayout_4->setObjectName(QStringLiteral("gridLayout_4"));
        driveList = new QComboBox(FilePanel);
        driveList->setObjectName(QStringLiteral("driveList"));

        gridLayout_4->addWidget(driveList, 0, 0, 1, 1);

        fileList = new QTreeView(FilePanel);
        fileList->setObjectName(QStringLiteral("fileList"));

        gridLayout_4->addWidget(fileList, 1, 0, 1, 1);


        gridLayout_2->addWidget(FilePanel, 0, 0, 1, 1);

        DisplayPanel = new QGroupBox(horizontalGroupBox);
        DisplayPanel->setObjectName(QStringLiteral("DisplayPanel"));
        DisplayPanel->setMinimumSize(QSize(400, 0));
        gridLayout_3 = new QGridLayout(DisplayPanel);
        gridLayout_3->setSpacing(6);
        gridLayout_3->setContentsMargins(11, 11, 11, 11);
        gridLayout_3->setObjectName(QStringLiteral("gridLayout_3"));
        graphicsView = new QGraphicsView(DisplayPanel);
        graphicsView->setObjectName(QStringLiteral("graphicsView"));

        gridLayout_3->addWidget(graphicsView, 0, 1, 1, 1);


        gridLayout_2->addWidget(DisplayPanel, 0, 1, 1, 1);

        ToolsPanel = new QGroupBox(horizontalGroupBox);
        ToolsPanel->setObjectName(QStringLiteral("ToolsPanel"));
        ToolsPanel->setMinimumSize(QSize(100, 0));
        ToolsPanel->setMaximumSize(QSize(100, 16777215));
        gridLayout_5 = new QGridLayout(ToolsPanel);
        gridLayout_5->setSpacing(6);
        gridLayout_5->setContentsMargins(11, 11, 11, 11);
        gridLayout_5->setObjectName(QStringLiteral("gridLayout_5"));
        pushButton = new QPushButton(ToolsPanel);
        pushButton->setObjectName(QStringLiteral("pushButton"));
        QSizePolicy sizePolicy(QSizePolicy::Fixed, QSizePolicy::Fixed);
        sizePolicy.setHorizontalStretch(0);
        sizePolicy.setVerticalStretch(0);
        sizePolicy.setHeightForWidth(pushButton->sizePolicy().hasHeightForWidth());
        pushButton->setSizePolicy(sizePolicy);

        gridLayout_5->addWidget(pushButton, 2, 1, 1, 1);

        groupBox = new QGroupBox(ToolsPanel);
        groupBox->setObjectName(QStringLiteral("groupBox"));
        groupBox->setMaximumSize(QSize(16777215, 100));
        gridLayout_6 = new QGridLayout(groupBox);
        gridLayout_6->setSpacing(6);
        gridLayout_6->setContentsMargins(11, 11, 11, 11);
        gridLayout_6->setObjectName(QStringLiteral("gridLayout_6"));
        horizontalSlider = new QSlider(groupBox);
        horizontalSlider->setObjectName(QStringLiteral("horizontalSlider"));
        sizePolicy.setHeightForWidth(horizontalSlider->sizePolicy().hasHeightForWidth());
        horizontalSlider->setSizePolicy(sizePolicy);
        horizontalSlider->setOrientation(Qt::Horizontal);

        gridLayout_6->addWidget(horizontalSlider, 1, 0, 1, 1);

        label = new QLabel(groupBox);
        label->setObjectName(QStringLiteral("label"));

        gridLayout_6->addWidget(label, 0, 0, 1, 1);

        label_2 = new QLabel(groupBox);
        label_2->setObjectName(QStringLiteral("label_2"));

        gridLayout_6->addWidget(label_2, 2, 0, 1, 1);

        horizontalSlider_2 = new QSlider(groupBox);
        horizontalSlider_2->setObjectName(QStringLiteral("horizontalSlider_2"));
        sizePolicy.setHeightForWidth(horizontalSlider_2->sizePolicy().hasHeightForWidth());
        horizontalSlider_2->setSizePolicy(sizePolicy);
        horizontalSlider_2->setOrientation(Qt::Horizontal);

        gridLayout_6->addWidget(horizontalSlider_2, 3, 0, 1, 1);


        gridLayout_5->addWidget(groupBox, 0, 1, 1, 1);


        gridLayout_2->addWidget(ToolsPanel, 0, 2, 1, 1);

        gridLayout_2->setColumnStretch(0, 3);
        gridLayout_2->setColumnStretch(1, 6);
        gridLayout_2->setColumnStretch(2, 2);

        gridLayout->addWidget(horizontalGroupBox, 0, 0, 1, 1);

        MainWindow->setCentralWidget(centralWidget);

        retranslateUi(MainWindow);

        QMetaObject::connectSlotsByName(MainWindow);
    } // setupUi

    void retranslateUi(QMainWindow *MainWindow)
    {
        MainWindow->setWindowTitle(QApplication::translate("MainWindow", "Raw Parser", 0));
        FilePanel->setTitle(QApplication::translate("MainWindow", "File Selector", 0));
        DisplayPanel->setTitle(QApplication::translate("MainWindow", "Image", 0));
        ToolsPanel->setTitle(QApplication::translate("MainWindow", "Tools", 0));
        pushButton->setText(QApplication::translate("MainWindow", "Save as JPEG", 0));
        groupBox->setTitle(QApplication::translate("MainWindow", "Modification", 0));
        label->setText(QApplication::translate("MainWindow", "White Balance:", 0));
        label_2->setText(QApplication::translate("MainWindow", "Saturation:", 0));
    } // retranslateUi

};

namespace Ui {
    class MainWindow: public Ui_MainWindow {};
} // namespace Ui

QT_END_NAMESPACE

#endif // UI_MAINWINDOW_H
