using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Collections.Generic;
using Microsoft.VisualBasic;
using Module.UI.ViewCom;

using Common.Utility.CLR;
using Common.Platform;
using Module.UI.DataCount.SupplyInformation;
using Module.UI.Model;
using Module.UI.Model.Collections;
using Module.Utility.CLR;
using Module.UI.Utility;
using Module.UI.Interface;
using Module.UI.DataCount;
using Option.Utility.CLR.VFare;
using Option.CoreModule.CLR;
using Option.UI.DataCount;
using Module.Platform.Jobs.ICA.CLR;

namespace Option.UI.Utility
{
    /// <summary>
    /// プリンタデータ作成用データ
    /// </summary>
    public class PrintDataCreator : IPrintDataCreator
    {
        #region 内部定義
        /// <summary>再印字、再出力用ファイル出力先(現行日計)</summary>
        private static readonly string DayCurDir = SettingData.Data1 + "data_count\\day\\cur\\";
        /// <summary>再印字、再出力用ファイル出力先（現行累計）</summary>
        private static readonly string TotalCurDir = SettingData.Data1 + "data_count\\total\\cur\\";
        /// <summary>再印字、再出力用ファイル出力先(未送日計)</summary>
        private static readonly string DayOldDir = SettingData.Data1 + "data_count\\day\\old1\\";
        /// <summary>再印字、再出力用ファイル出力先（未送累計）</summary>
        private static readonly string TotalOldDir = SettingData.Data1 + "data_count\\total\\old1\\";
        /// <summary>QRデータファイル出力先</summary>
        private static readonly string QrDir = SettingData.Temp + "QR\\";
        /// <summary>QRデータファイル出力先</summary>
        private static readonly string QrTempDir = SettingData.Temp + "QR\\Temp\\";
         /// <summary>オレカ発行機関</summary>
        private static readonly int[] OrangeOrgan = { 1, 2, 3, 4, 5, 6, 7, 0 };
        /// <summary>イオカ発行機関</summary>
        private static readonly int[] IoOrgan = { 13, 0 };
        /// <summary>スイカ発行機関</summary>
        private static readonly int[] SuicaOrgan = { 23, 25, 125, 123, 24, 22, 27, 127, 227, 124, 0 };
        /// <summary> 取引情報の参照 </summary>
        private Session session = DataStore.GetInstance().Session;

        /// <summary>集計データ種別</summary>
        public enum TotalKindType
        {
            /// <summary>現行データ</summary>
            Current = 1,
            /// <summary>前回データ</summary>
            Old1,
            /// <summary>前々回データ</summary>
            Old2,
        }

        /// <summary>締切データ種別</summary>
        public enum DataKindType
        {
            /// <summary>日計</summary>
            Day = 0,
            /// <summary>累計</summary>
            Total,
        }

        /// <summary>JP印字種類フラグ </summary>
        public enum CHANGEKIND
        {
            /// <summary>記名交換 </summary>
            NameChange = 1,
            /// <summary>物販交換 </summary>
            MoneyChange = 2,
            /// <summary>世代交換 </summary>
            Change = 3,
            /// <summary>属性変更 </summary>
            ICAttributeChange = 4,
            /// <summary>発行替え </summary>
            Exchange = 5
        }

        /// <summary>駅種別 </summary>
        public enum StationType
        {
            /// <summary> 発駅 </summary>
            HatsuCode = 0,

            /// <summary> 着駅 </summary>
            TyakuCode,

            /// <summary> グリーン発駅</summary>
            GreenHatsuCode,

            /// <summary> グリーン着駅</summary>
            GreenTyakuCode,

            /// <summary> 新幹線発駅</summary>
            ShinkansenHatsuCode,

            /// <summary> 新幹線着駅</summary>
            ShinkansenTyakuCode,

            /// <summary> 特急区間経由指定駅</summary>
            ExpressKeiyu,
        }

        /// <summary> 保守設定情報の参照 </summary>
        private MaintenanceSettings settings = DataStore.GetInstance().MaintenanceSettings;

        /// <summary>プリンタデータリスト </summary>
        private List<string> PrintData { get; set; }

        /// <summary>締切再出力データリスト </summary>
        private List<PrintDataCreator> PrintDataCreatorList { get; set; }

        /// <summary> 集計情報の参照（日計/累計） </summary>
        private TotalizeDataSearch TotalizeDayData { get; set; }

        /// <summary> 集計情報の参照（累計） </summary>
        private TotalizeDataSearch TotalizeTotalData { get; set; }

        /// <summary> QRコード用集計情報の参照（日計/累計） </summary>
        private TotalizeDataSearch TotalizeData { get; set; }

        /// <summary> トラブルデ情報の参照 </summary>
        private TotalizeDataSearch TroubleDataItem { get; set; }

        /// <summary>係員操作情報参照 </summary>
        private OperatorInfo operatorInfo = DataStore.GetInstance().OperatorInfo;

        /// <summary> 機器情報の参照 </summary>
        private MachineInfo machineInfo = DataStore.GetInstance().MachineInfo;

        /// <summary>カード識別フラグ </summary>
        private enum CARD_KIND
        {
            /// <summary>オレンジカード </summary>
            ORANGE = 1,
            /// <summary>磁気イオカード </summary>
            MAGNETIC_IO = 2,
            /// <summary>社線ＳＦカード </summary>
            LINE_SF = 3,
            /// <summary>ＩＣカード </summary>
            IC_CARD = 4,
        }

        /// <summary>金券／チャージフラグ </summary>
        private enum MONEY_OR_CHARGE
        {
            /// <summary>金券 </summary>
            MONEY = 0,
            /// <summary>チャージ </summary>
            CHARGE = 1,
        }

        /// <summary>大人／小児フラグ </summary>
        private enum ADULT_OR_CHIKD
        {
            /// <summary>大人 </summary>
            ADULT = 1,
            /// <summary>小児 </summary>
            CHILD = 2,
        }

        /// <summary> 共通ヘッダ部 </summary>
        private string CommonHeader { get; set; }
        #endregion

        #region コンストラクタ
        /// <summary>
        /// コンストラクタ
        /// </summary>
        public PrintDataCreator()
        {
            PrintData = new List<string>();
            PrintDataCreatorList = new List<PrintDataCreator>();
        }
        #endregion

        #region プリンタデータ作成インタフェース
        /// <summary>
        /// RP印字情報作成データ
        /// </summary>
        /// <param name="printNum">出力:印字回数</param>
        /// <param name="ticketOutput">出力:RP印字情報</param>
        public void RpPrintCreateData(out int printNum, out byte[] ticketOutput)
        {
            PrintDataCreatorList.Clear();
            PrintData.Clear();
            ticketOutput = null;
            printNum = 0;
            int printCount = 0;
            bool accountPrint = false;
            string saveFilePath = string.Empty;
            DataKindType dataKind = DataKindType.Day;
            // 印字プロパティ取得（出力葉数、締切データ種別、口座別売上プリント有無）
            GetPrintDataProperty(out printCount, out dataKind, out accountPrint, out saveFilePath);
            // 出力データを保存する
            if (SavePrintData(dataKind, accountPrint, saveFilePath))
            {
                // 印字用データを作成
                printNum = CreatePrintData(printCount, dataKind, accountPrint);
                if (PrintDataCreatorList != null && printNum > 0)
                {
                    if (PrintDataCreatorList[0].PrintData != null && PrintDataCreatorList[0].PrintData.Count > 0)
                    {
                        // 締切データ１枚目タイトルをインサートする
                        PrintDataCreatorList[0].PrintData.Insert(0, string.Empty);
                        PrintDataCreatorList[0].PrintData.Insert(1, "締切データ（駅控）");
                        PrintDataCreatorList[0].PrintData.Insert(2, string.Empty);
                        var freeFormatInput = new RpFreeFormatInput();
                        if (operatorInfo.IsQrMode)
                        {
                            // 偶数回目(２回目、４回目)にQRコード印字できるため
                            printNum = printNum * 2;
                            freeFormatInput.PreFeed = 0;
                            freeFormatInput.CutKind = RpFreeFormatInput.PaperCutKindType.NON_CUT;
                        }
                        else
                        {
                            freeFormatInput.PreFeed = 8;
                            freeFormatInput.CutKind = RpFreeFormatInput.PaperCutKindType.PARTIAL;
                        }
                        freeFormatInput.RawMode = false;
                        freeFormatInput.PostFeed = 0;
                        freeFormatInput.PrintInfo = (string[])PrintDataCreatorList[0].PrintData.ToArray();
                        var RpMediaInput = new MediaDataInput();
                        RpMediaInput.EditKind = MediaDataInput.MediaEditKind.EditRpFreeFormat;
                        RpMediaInput.Info = freeFormatInput.GetBytes();
                        ticketOutput = RpMediaInput.GetBytes();
                    }
                }
            }
        }

        /// <summary>
        /// RP印字情報作成データ（つづきの印字）
        /// </summary>
        /// <param name="count">印字カウンタ数</param>
        /// <param name="ticketOutput">出力:RP印字情報</param>
        public void RpContinuePrintCreateData(int count, out byte[] ticketOutput)
        {
            ticketOutput = null;
            string totalPath = Path.Combine(TotalCurDir, DataStore.GetInstance().CommonSettings.SaveQrClosingFile);
            string dayPath = Path.Combine(DayCurDir + DataStore.GetInstance().CommonSettings.SaveQrClosingFile);
            if (operatorInfo.IsQrMode && (File.Exists(totalPath) || File.Exists(dayPath)))
            {
                // 奇数回目印字は締切データを編集(count = 2)
                if (count % 2 == 0)
                {
                    // 奇数回目印字:countは偶数
                    // 締切データ番号を計算する
                    int dataNum = count / 2;
                    if (PrintDataCreatorList != null && PrintDataCreatorList.Count > dataNum)
                    {
                        // 締切データ２枚目タイトルをインサートする
                        PrintDataCreatorList[dataNum].PrintData.Insert(0, string.Empty);
                        PrintDataCreatorList[dataNum].PrintData.Insert(1, "締切データ（月報送付用）");
                        PrintDataCreatorList[dataNum].PrintData.Insert(2, string.Empty);
                        var freeFormatInput = new RpFreeFormatInput();
                        freeFormatInput.PreFeed = 0;
                        freeFormatInput.CutKind = RpFreeFormatInput.PaperCutKindType.NON_CUT;
                        freeFormatInput.RawMode = false;
                        freeFormatInput.PostFeed = 0;
                        freeFormatInput.PrintInfo = (string[])PrintDataCreatorList[dataNum].PrintData.ToArray();
                        var RpMediaInput = new MediaDataInput();
                        RpMediaInput.EditKind = MediaDataInput.MediaEditKind.EditRpFreeFormat;
                        RpMediaInput.Info = freeFormatInput.GetBytes();
                        ticketOutput = RpMediaInput.GetBytes();
                    }
                }
                else
                {
                    // 偶数回目印字はＱＲコードを編集(count=1,3)
                    // 偶数回目印字:countは奇数
                    ticketOutput = EditRpQRData(false).GetBytes();
                }
            }
            else
            {
                if (PrintDataCreatorList != null && PrintDataCreatorList.Count > count)
                {
                    if (PrintDataCreatorList[count].PrintData != null && PrintDataCreatorList[count].PrintData.Count > 0)
                    {
                        // 締切データ２枚目タイトルをインサートする
                        PrintDataCreatorList[count].PrintData.Insert(0, string.Empty);
                        PrintDataCreatorList[count].PrintData.Insert(1, "締切データ（月報送付用）");
                        PrintDataCreatorList[count].PrintData.Insert(2, string.Empty);
                        var freeFormatInput = new RpFreeFormatInput();
                        freeFormatInput.RawMode = false;
                        freeFormatInput.CutKind = RpFreeFormatInput.PaperCutKindType.PARTIAL;
                        freeFormatInput.PreFeed = 8;
                        freeFormatInput.PostFeed = 0;
                        freeFormatInput.PrintInfo = (string[])PrintDataCreatorList[count].PrintData.ToArray();
                        var mediaInput = new MediaDataInput();
                        mediaInput.EditKind = MediaDataInput.MediaEditKind.EditRpFreeFormat;
                        mediaInput.Info = freeFormatInput.GetBytes();
                        ticketOutput = mediaInput.GetBytes();
                    }
                }
            }
        }

        /// <summary>
        /// RP再印字情報作成データ
        /// </summary>
        /// <param name="printNum">出力:印字回数</param>
        /// <param name="ticketOutput">出力:印字情報</param>
        public void RpPrintRetryCreateData(out int printNum, out byte[] ticketOutput)
        {
            ticketOutput = null;
            printNum = 0;
            // 印字データクリア
            ClearPrintData();
            // 前回RP締切出力
            ReprintDataEdit();
            if (PrintData != null && PrintData.Count > 0)
            {
                // 締切データ再出力タイトルをインサートする
                PrintData.Insert(0, string.Empty);
                PrintData.Insert(1, "締切データ（再出力）");
                PrintData.Insert(2, string.Empty);
                // 前回締切出力チェック
                string dayPath = Path.Combine(DayOldDir, DataStore.GetInstance().CommonSettings.SaveQrClosingFile);
                string totalPath = Path.Combine(TotalOldDir, DataStore.GetInstance().CommonSettings.SaveQrClosingFile);

                var freeFormatInput = new RpFreeFormatInput();
                if ((operatorInfo.RpRetryKind == OperatorInfo.RpRetryType.RpDay && File.Exists(dayPath)) ||
                    (operatorInfo.RpRetryKind == OperatorInfo.RpRetryType.RpTotal && File.Exists(totalPath)))
                {
                    // プリンタ再出力データを編集
                    printNum = 2;
                    freeFormatInput.CutKind = RpFreeFormatInput.PaperCutKindType.NON_CUT;
                    freeFormatInput.PreFeed = 0;
                }
                else
                {
                    printNum = 1;
                    freeFormatInput.CutKind = RpFreeFormatInput.PaperCutKindType.PARTIAL;
                    freeFormatInput.PreFeed = 8;
                }
                freeFormatInput.RawMode = false;
                freeFormatInput.PostFeed = 0;
                freeFormatInput.PrintInfo = (string[])PrintData.ToArray();
                var mediaInput = new MediaDataInput();
                mediaInput.EditKind = MediaDataInput.MediaEditKind.EditRpFreeFormat;
                mediaInput.Info = freeFormatInput.GetBytes();
                ticketOutput = mediaInput.GetBytes();
            }
        }

        /// <summary>
        /// RP再印字情報(QRコード)作成データ
        /// </summary>
        /// <param name="ticketOutput">出力:印字情報</param>
        public void RpPrintRetryCreateQrData(out byte[] ticketOutput)
        {
            ticketOutput = null;
            string dayPath = Path.Combine(DayOldDir, DataStore.GetInstance().CommonSettings.SaveQrClosingFile);
            string totalPath = Path.Combine(TotalOldDir, DataStore.GetInstance().CommonSettings.SaveQrClosingFile);
            if ((operatorInfo.RpRetryKind == OperatorInfo.RpRetryType.RpDay && File.Exists(dayPath)) ||
                (operatorInfo.RpRetryKind == OperatorInfo.RpRetryType.RpTotal && File.Exists(totalPath)))
            {
                ticketOutput = EditRpQRData(true).GetBytes();
            }
        }

        /// <summary>
        /// QR出力データを編集
        /// </summary>
        /// <param name="reisPrintRetry">true:プリンタ再出力</param>
        /// <returns>QR出力データ</returns>
        private MediaDataInput EditRpQRData(bool reisPrintRetry)
        {
            List<string> printData = new List<string>();
            printData.Add("≪≪≪≪≪≪券売機≫≫≫≫≫≫");
            printData.Add("以下のＱＲコードを駅収管理端末");
            printData.Add("のスキャナで読み込んで下さい。");
            printData.Add("≪≪≪≪≪≪注　意≫≫≫≫≫≫");
            printData.Add("ＱＲコード部分を折り曲げないで");
            printData.Add("下さい。万一、汚損した場合には");
            printData.Add("再出力を行って下さい。　　　　");
            printData.Add(string.Empty);
            printData.Add("----------＜開　始＞----------");
            var QrMediaInput = new MediaDataInput();
            string[] PostPrintData = new string[1];
            var QRDataInput = new RpQRDataInput();
            QRDataInput.RawMode = false;
            QRDataInput.PrePrintInfo = (string[])printData.ToArray();
            if (reisPrintRetry)
            {
                if (operatorInfo.RpRetryKind == OperatorInfo.RpRetryType.RpDay)
                {
                    QRDataInput.QRData = File.ReadAllBytes(DayOldDir + DataStore.GetInstance().CommonSettings.SaveQrClosingFile);
                }
                else
                {
                    QRDataInput.QRData = File.ReadAllBytes(TotalOldDir + DataStore.GetInstance().CommonSettings.SaveQrClosingFile);
                }
            }
            else
            {
                if (operatorInfo.ClosePattern == OperatorInfo.ClosePatternType.RpCloseDay)
                {
                    QRDataInput.QRData = File.ReadAllBytes(DayCurDir + DataStore.GetInstance().CommonSettings.SaveQrClosingFile);
                }
                else
                {
                    QRDataInput.QRData = File.ReadAllBytes(TotalCurDir + DataStore.GetInstance().CommonSettings.SaveQrClosingFile);
                }
            }
            PostPrintData[0] = "----------＜終　了＞----------";
            QRDataInput.PostPrintInfo = PostPrintData;
            QRDataInput.PreFeed = 8;
            QRDataInput.CutKind = RpFreeFormatInput.PaperCutKindType.PARTIAL;
            QRDataInput.PostFeed = 0;
            QRDataInput.BackupPath = SettingData.Temp;
            // 印字作成種別設定
            QrMediaInput.EditKind = MediaDataInput.MediaEditKind.EditRpQRData;
            QrMediaInput.Info = QRDataInput.GetBytes();
            return QrMediaInput;
        }

        /// <summary>
        /// RP印字情報作成データ
        /// </summary>
        /// <param name="ticketOutput">出力:RP印字情報</param>
        public void MaintenanceRpPrintCreateData(out byte[] ticketOutput)
        {
            ticketOutput = null;
            MaintenanceInfo Info = DataStore.GetInstance().MaintenanceInfo;
            // 印字用データを作成
            if (Info.PrintInfo != null && Info.PrintInfo.Length > 0)
            {
                string[] printData = new string[Info.PrintInfo.Length];
                int index = 0;
                foreach (var data in Info.PrintInfo)
                {
                    printData[index] = data;
                    index++;
                }

                var freeFormatInput = new RpFreeFormatInput();
                freeFormatInput.RawMode = false;
                freeFormatInput.CutKind = RpFreeFormatInput.PaperCutKindType.PARTIAL;
                freeFormatInput.PreFeed = 8;
                freeFormatInput.PostFeed = 0;
                freeFormatInput.PrintInfo = printData;
                var mediaInput = new MediaDataInput();
                mediaInput.EditKind = MediaDataInput.MediaEditKind.EditRpFreeFormat;
                mediaInput.Info = freeFormatInput.GetBytes();
                ticketOutput = mediaInput.GetBytes();
            }
        }

        /// <summary>
        /// オフライン出力ファイル削除
        /// </summary>
        public void DeleteOfflineSaveFile()
        {
            string path = string.Empty;
            string qrcodePath = string.Empty;
            switch (operatorInfo.ClosePattern)
            {
                case OperatorInfo.ClosePatternType.RpCloseDay:
                    path = Path.Combine(DayCurDir, DataStore.GetInstance().CommonSettings.SaveRpClosingFile);
                    qrcodePath = Path.Combine(DayCurDir, DataStore.GetInstance().CommonSettings.SaveQrClosingFile);
                    break;
                case OperatorInfo.ClosePatternType.RpCloseTotal:
                    path = Path.Combine(TotalCurDir, DataStore.GetInstance().CommonSettings.SaveRpClosingFile);
                    qrcodePath = Path.Combine(TotalCurDir, DataStore.GetInstance().CommonSettings.SaveQrClosingFile);
                    break;
                default:
                    break;
            }
            if (path.Length > 0)
            {
                try
                {
                    MirrorController.RemoveFile(path);
                }
                catch (MirrorControllerException mirrorControllerException)
                {
                    MirrorController.ResultType mirroredResult = mirrorControllerException.Result;
                }
            }
            if (qrcodePath.Length > 0)
            {
                try
                {
                    MirrorController.RemoveFile(qrcodePath);
                }
                catch (MirrorControllerException mirrorControllerException)
                {
                    MirrorController.ResultType mirroredResult = mirrorControllerException.Result;
                }
            }
        }

        /// <summary>
        /// QRコード印字内容を作成
        /// </summary>
        public void TryCreateQrCode()
        {
            // 締切キー状態を取得する
            DataKindType kind = new DataKindType();
            if (operatorInfo.ClosePattern == OperatorInfo.ClosePatternType.RpCloseDay)
            {
                kind = DataKindType.Day;
            }
            else if (operatorInfo.ClosePattern == OperatorInfo.ClosePatternType.RpCloseTotal)
            {
                kind = DataKindType.Total;
            }
            operatorInfo.QrResult = CreateQrData(kind, DataStore.GetInstance().CommonSettings.SaveQrClosingFile);
            if (operatorInfo.QrResult == OperatorInfo.QrResultType.QrSuccess)
            {
                operatorInfo.IsQrMode = true;
            }
            else
            {
                operatorInfo.IsQrMode = false;
            }
        }

        /// <summary>
        /// QRコードファイル二重化
        /// </summary>
        /// <returns>True:成功</returns>
        public bool QrCodeBackup()
        {
            bool ret = true;
            string FromPath = QrTempDir + DataStore.GetInstance().CommonSettings.SaveQrClosingFile;
            string OutputPath = string.Empty;
            if (operatorInfo.ClosePattern == OperatorInfo.ClosePatternType.RpCloseDay)
            {
                OutputPath = DayCurDir;
            }
            else
            {
                OutputPath = TotalCurDir;
            }
            byte[] buffer = new byte[File.ReadAllBytes(FromPath).Length];
            try
            {
                MirrorController.CreateDirectory(OutputPath);
                string path = Path.Combine(OutputPath, DataStore.GetInstance().CommonSettings.SaveQrClosingFile);
                MirroredFileStream mirroredStream = MirrorController.GetMirroredFileStream(path, FileMode.Create);
                using (BinaryReader reader = new BinaryReader(new FileStream(FromPath, FileMode.Open)))
                {
                    using (BinaryWriter writer = new BinaryWriter(mirroredStream))
                    {
                        // 元ファイルから読込
                        while (reader.Read(buffer, 0, buffer.Length) > 0)
                        {
                            // 先ファイルへ書込
                            writer.Write(buffer, 0, buffer.Length);
                        }
                        writer.Flush();
                        writer.Close();
                    }
                    reader.Close();
                }
                mirroredStream.CreateCheckFile();
                mirroredStream.Close();
            }
            catch (MirrorControllerException mirrorControllerException)
            {
                MirrorController.ResultType mirroredResult = mirrorControllerException.Result;
                ret = false;
            }
            File.Delete(FromPath);
            return ret;
        }
        #endregion

        #region プリンタデータ作成インタフェース（保守モード機能：集計データ出力用）
        /// <summary>
        /// 保守モード機能：集計のプリンタ出力用データの作成
        /// </summary>
        /// <param name="totalKind">運賃世代</param>
        /// <param name="printdata">出力データ</param>
        public void RpPrintCreateDataForMaintenance(DataCountBase.Generation totalKind, out string[] printdata)
        {
            printdata = new string[0];
            // 印刷用データの編集処理
            var printaDataCreate = new PrintDataCreator();
            if (settings.MultiFunctionVenderType)
            {
                printaDataCreate.MultiClosingDataEdit(totalKind);
            }
            else
            {
                printaDataCreate.ClosingDataEdit(totalKind, DataKindType.Day, 0, true);
            }
            // 印字用データを出力パラメータに設定
            if (printaDataCreate.PrintData != null && printaDataCreate.PrintData.Count > 0)
            {
                // 集計データタイトルをインサートする
                // 空き行を追加
                printaDataCreate.PrintData.Insert(0, string.Empty);
                if (totalKind == DataCountBase.Generation.Cur)
                {
                    printaDataCreate.PrintData.Insert(1, "集計データ（現）");
                }
                else if (totalKind == DataCountBase.Generation.Old1)
                {
                    printaDataCreate.PrintData.Insert(1, "集計データ（前）");
                }
                // 空き行を追加
                printaDataCreate.PrintData.Insert(2, string.Empty);
                printdata = new string[printaDataCreate.PrintData.Count];
                int index = 0;
                foreach (var data in printaDataCreate.PrintData)
                {
                    printdata[index] = data;
                    index++;
                }
            }
        }
        #endregion

        #region 内部メソッド(締切データ編集)
        /// <summary>
        /// 売上げデータ印字編集
        /// </summary>
        /// <param name="kind">データ集計種別</param>
        /// <param name="dataKind">締切データ種別</param>
        /// <param name="closeNo">締切Ｎｏ．指定（=0:印字なし）</param>
        /// <param name="accountPrint">口座別データ印字指定true:有</param>
        private void ClosingDataEdit(DataCountBase.Generation kind, DataKindType dataKind, int closeNo = 0, bool accountPrint = true)
        {
            // 集計情報の参照
            TotalizeDayData = new TotalizeDataSearch(TotalizeDataCount.GetInstance().GetData(kind, TotalizeDataCount.TotalizeKind.Day, DataCountLabels.CategoryPrintData));
            TotalizeTotalData = new TotalizeDataSearch(TotalizeDataCount.GetInstance().GetData(kind, TotalizeDataCount.TotalizeKind.Total, DataCountLabels.CategoryPrintData));
            // トラブルデータ取得
            TroubleDataItem = new TotalizeDataSearch(TroubleDataCount.GetInstance().GetData(kind, DataCountLabels.CategoryTroubleData));

            // 日付、時刻、クリア回数
            // 設置駅コード、号機
            ClosingDataEditForTitle(closeNo);

            // 総売上枚数／金額
            ClosingDataEditForTotalSale();

            ulong sumCouponDay = 0;   // 回数券発売金額（日計）
            ulong sumCouponTotal = 0; // 回数券発売金額（累計）
            if (DataStore.GetInstance().MaintenanceSettings.VenderMode.Value != MaintenanceSettings.VenderModeType.IcType)
            {
                // 口座別発売金額/枚数
                ClosingDataEditForAccountSale(dataKind, accountPrint, out sumCouponDay, out sumCouponTotal);
            }
            else
            {
                // 空行
                PrintData.Add(string.Empty);
            }

            // 取扱データ
            ClosingDataEditForTrade();

            // 現金
            ClosingDataEditForCash();

            if (DataStore.GetInstance().MaintenanceSettings.SfCardUsable.Value)
            {
                // オレカ
                ClosingDataEditForOrangeCard();
            }

            if (DataStore.GetInstance().MaintenanceSettings.PpCardUsable.Value)
            {
                // イオカ
                ClosingDataEditForIoCard();
            }

            if (DataStore.GetInstance().MaintenanceSettings.IcCardUsable.Value)
            {
                // スイカ
                ClosingDataEditForSuica();
            }

            // 自社他社区分
            ClosingDataEditForOtherCompany();

            // 回数券
            ClosingDataEditForCoupon(sumCouponDay, sumCouponTotal);

            // スイカチャージ
            ClosingDataEditForSuicaCharge();

            // スイカデポジット
            ClosingDataEditForSuicaDeposit();

            // 総売上
            ClosingDataEditForTotal();

            // スイカ移替
            ClosingDataEditForOrganChange();

            // 誤購入払戻
            ClosingDataEditForRefund();

            // ＳＦ返金：保守設定「ＳＦ返金機能」機能あり の場合のみ表示
            ClosingDataEditForSfBack();

            // ポイントチャージ：保守設定「ポイントチャージ機能」機能あり の場合のみ表示
            ClosingDataEditForPointCharge();
        }

        /// <summary>
        /// 売上げデータ印字編集(多機能券売機用)
        /// </summary>
        /// <param name="kind">データ集計種別</param>
        private void MultiClosingDataEdit(DataCountBase.Generation kind)
        {
            // 集計情報の参照
            TotalizeDayData = new TotalizeDataSearch(TotalizeDataCount.GetInstance().GetData(kind, TotalizeDataCount.TotalizeKind.Day, DataCountLabels.CategoryPrintData));

            // タイトル、管理日計日
            // 設置駅コード、コーナ、号機
            ClosingDataEditForMultiTitle();

            // きっぷ枚数／金額
            ClosingDataEditForTiecket();

            // 定期券
            ClosingDataEditForPass();

            // チャージ：保守設定「ＩＣカード使用設定」機能あり の場合のみ表示
            ClosingDataEditForCharge();

            // デポジット徴収：保守設定「ＩＣカード使用設定」機能あり の場合のみ表示
            ClosingDataEditForDiposit();

            // デポジット返却：保守設定「ＩＣカード使用設定」機能あり の場合のみ表示
            ClosingDataEditForDipositRest();

            // Ｓｕｉｃａ：保守設定「ＩＣカード使用設定」機能あり の場合のみ表示
            ClosingDataEditForIC();

            // 回収枚数
            ClosingDataEditForCollect();

            // 試刷枚数
            ClosingDataEditForTest();

            // 金庫枚数
            ClosingDataEditForCoffer();

            // 単発枚数
            ClosingDataEditForSingle();

            // 取忘枚数
            ClosingDataEditForForget();

            // ＳＦ返金
            ClosingDataEditForSfback();

            // ポイントチャージ
            ClosingDataEditForPoint();
        }

        /// <summary>
        /// 印字データファイル保存
        /// </summary>
        /// <param name="filePath">パス</param>
        /// <returns>True:成功</returns>
        private bool SavePrintData(string filePath)
        {
            bool ret = true;
            try
            {
                MirrorController.CreateDirectory(filePath);
                string path = Path.Combine(filePath, DataStore.GetInstance().CommonSettings.SaveRpClosingFile);
                MirroredFileStream mirroredStream = MirrorController.GetMirroredFileStream(path, FileMode.Create);
                using (StreamWriter writer = new StreamWriter(mirroredStream, MirrorController.FileEncoding))
                {
                    if (PrintData != null)
                    {
                        foreach (var data in PrintData)
                        {
                            writer.WriteLine(data);
                        }
                    }
                    writer.Close();
                }
                mirroredStream.CreateCheckFile();
                mirroredStream.Close();
            }
            catch (MirrorControllerException mirrorControllerException)
            {
                MirrorController.ResultType mirroredResult = mirrorControllerException.Result;
                ret = false;
            }
            return ret;
        }

        /// <summary>
        /// データ再印字編集
        /// </summary>
        private void ReprintDataEdit()
        {
            string filePath = string.Empty;
            if (operatorInfo.RpRetryKind == OperatorInfo.RpRetryType.RpTotal)
            {
                // RP締切（累計）
                filePath = TotalOldDir;
            }
            else
            {
                // RP締切（日計）
                filePath = DayOldDir;
            }
            string path = Path.Combine(filePath, DataStore.GetInstance().CommonSettings.SaveRpClosingFile);

            using (FileStream file = new FileStream(path, FileMode.Open, FileAccess.Read))
            {
                using (StreamReader reader = new StreamReader(file, System.Text.Encoding.GetEncoding("shift-jis")))
                {
                    while (!reader.EndOfStream)
                    {
                        string value = reader.ReadLine();
                        PrintData.Add(value);
                    }
                }
            }
        }

        /// <summary>
        /// 印字データクリア
        /// </summary>
        private void ClearPrintData()
        {
            PrintData.Clear();
        }

        /// <summary>
        /// 日付、時刻、クリア回数および設置駅コード、号機
        /// </summary>
        /// <param name="closeNo">締切Ｎｏ．指定</param>
        private void ClosingDataEditForTitle(int closeNo)
        {
            // 日付、時刻、クリア回数
            System.DateTime currentTime = System.DateTime.Now;
            string printData = string.Format("{0:D4}年{1:D2}月{2:D2}日　{3:D2}：{4:D2}　　", currentTime.Year, currentTime.Month, currentTime.Day, currentTime.Hour, currentTime.Minute);
            if (closeNo > 0)
            {
                // クリア回数印字指定あり
                long ClearCount = DataStore.GetInstance().ClosingNumber.GetClosingNumber(closeNo);
                printData = printData + string.Format("{0,2}回", ClearCount % 100);
            }
            else
            {
                printData = printData + "　　";
            }
            PrintData.Add(printData);
            // 設置駅コード、号機
            string machineNo = DataStore.GetInstance().MaintenanceSettings.PrintNo.Value;
            FareInfo fareInfo;
            if (VFareManager.GetFare(GenerationType.Current, out fareInfo))
            {
                PrintData.Add(string.Format("　　　　　　　 {0:D7}　{1,2}号機", Convert.ToInt32(DataStore.GetInstance().MaintenanceSettings.HostCode.Value), Convert.ToInt32(machineNo)));
            }
            // 空行
            PrintData.Add(string.Empty);
        }

        /// <summary>
        /// タイトル、管理日計日、設置駅コードおよびコーナ、号機（多機能券売機用）
        /// </summary>
        private void ClosingDataEditForMultiTitle()
        {
            // タイトル
            PrintData.Add("＊＊＊＊　締　　切　＊＊＊＊＊");
            // 管理日計日
            Dictionary<string, long> categoryData = TotalizeDataCount.GetInstance().GetData(DataCountBase.Generation.Cur, TotalizeDataCount.TotalizeKind.Day, DataCountLabels.CategoryManagementDate);
            int year = 0;
            int month = 0;
            int day = 0;
            if (categoryData != null)
            {
                long value = 0;
                if (categoryData.TryGetValue(DataCountLabels.ManagementDate, out value))
                {
                    year = (int)(value / 10000);
                    month = (int)((value % 10000) / 100);
                    day = (int)(value % 100);
                }
            }
            string printData = string.Format("  {0:D4}年  {1:D2}月  {2:D2}日", year, month, day);
            PrintData.Add(printData);

            // 設置駅コード
            PrintData.Add(string.Format("　設置駅コード　 {0:D7}", Convert.ToInt32(settings.HostCode.Value)));
            // コーナ、号機
            string machineNo = settings.PrintNo.Value;
            string corner = settings.PrintCorner.Value;
            int cornerNo = int.Parse(corner);
            if (cornerNo >= 0 && cornerNo <= 9)
            {
                // 00～09の場合、「0」抜く一桁印字
                corner = cornerNo.ToString();
            }
            else if (cornerNo >= 10 && cornerNo <= 35)
            {
                // 10～35の場合、「A」～「Z」を転換して一桁印字
                char buf = (char)((int)'A' + cornerNo - 10);
                corner = buf.ToString();
            }
            else
            {
                // 上記以外の場合、ブランク印字
                corner = "　";
            }
            PrintData.Add(string.Format("　 {0}コーナ　{1:D2}号機", corner, Convert.ToInt32(machineNo)));
            // 空行
            PrintData.Add("＊＊＊＊＊＊＊＊＊＊＊＊＊＊＊");
            PrintData.Add(string.Empty);
        }

        /// <summary>
        /// 総売上枚数／金額
        /// </summary>
        private void ClosingDataEditForTotalSale()
        {
            // タイトル
            PrintData.Add("　　　　日計　　　　　　累計　");
            // 総売上枚数
            long day = TotalizeDayData.GetData(DataCountLabels.TotalSaleNum, 6);
            long total = TotalizeTotalData.GetData(DataCountLabels.TotalSaleNum, 6);
            PrintData.Add(string.Format("　　{0,6}枚　　　　{1,6}枚　", day, total));
            // 総売上金額
            day = TotalizeDayData.GetData(DataCountLabels.TotalSaleAmount, 8);
            total = TotalizeTotalData.GetData(DataCountLabels.TotalSaleAmount, 8);
            PrintData.Add(string.Format("　{0,8}円　　　{1,8}円　", day, total));
        }

        /// <summary>
        /// 口座別発売金額/枚数
        /// </summary>
        /// <param name="dataKind">締切データ種別</param>
        /// <param name="accountPrint">口座別データ印字指定</param>
        /// <param name="sumCouponDay">回数券発売金額（日計）</param>
        /// <param name="sumCouponTotal">回数券発売金額（累計）</param>
        private void ClosingDataEditForAccountSale(DataKindType dataKind, bool accountPrint, out ulong sumCouponDay, out ulong sumCouponTotal)
        {
            // 口座別編集
            sumCouponDay = 0;
            sumCouponTotal = 0;
            EditFareData editFareData = new EditFareData();
            IOrderedEnumerable<KeyValuePair<ulong, EditFareData.EditFare>> mapEditFareOrder = null;
            Dictionary<ulong, bool> mapSaleAccount = new Dictionary<ulong, bool>();

            if (!editFareData.GetSaleAccountData(GenerationType.Current, ref mapSaleAccount))
            {
                return;
            }
            if (!editFareData.GetSortedFare(GenerationType.Current, EditFareData.FareSortType.PrintSaleData, ref mapEditFareOrder))
            {
                return;
            }

            ulong oldCode = 0;
            ulong oldOrder = 0;
            // 合計大人発売枚数
            long sumAdult = 0;
            // 合計小人発売枚数
            long sumChild = 0;
            string outPut = string.Empty;

            foreach (var item in mapEditFareOrder)
            {
                bool isCoupon = false;
                // 発行枚数が複数枚の券種は回数券
                if (item.Value.RejectCount > 1)
                {
                    isCoupon = true;
                }
                ulong code = (item.Value.TicketNO * 1000) + item.Value.AccountNO;
                // 該当パターンでは発売されていない口座？
                bool result = false;
                if (!mapSaleAccount.TryGetValue(code, out result))
                {
                    continue;
                }
                else
                {
                    if (!result)
                    {
                        continue;
                    }
                }
                // プリント順序または印字券種コードに変化あり？
                if (oldOrder != item.Value.PrintOrder || oldCode != item.Value.PrintCode)
                {
                    if (outPut != string.Empty)
                    {
                        // 口座別データ印字ありならば
                        if (accountPrint)
                        {
                            PrintData.Add(outPut);
                            outPut = string.Empty;
                        }
                    }
                    sumAdult = sumChild = 0;
                    oldOrder = item.Value.PrintOrder;
                }
                // 印字券種コードに変化あり？
                if (oldCode != item.Value.PrintCode)
                {
                    // 口座別データ印字ありならば
                    if (accountPrint)
                    {
                        // 空行
                        PrintData.Add(string.Empty);
                        // 券種別タイトル行
                        // 小人の印字券種コードは大人の印字券種コード＋５０
                        PrintData.Add(string.Format("券種 {0:D3}　大人　券種 {1:D3}　小児", item.Value.PrintCode % 1000, (item.Value.PrintCode + 50) % 1000));
                        // 回数券は単位を冊にする
                        if (isCoupon)
                        {
                            PrintData.Add(string.Format("　　円／　　冊　　　円／　　冊"));
                        }
                        else
                        {
                            PrintData.Add(string.Format("　　円／　　枚　　　円／　　枚"));
                        }
                    }
                    oldCode = item.Value.PrintCode;
                }
                // 口座別発売金額、枚数
                {
                    string printDataTempAccount = string.Empty;
                    long workAdultDay = 0;
                    long workChildDay = 0;
                    long workAdultTotal = 0;
                    long workChildTotal = 0;
                    // 大人発売枚数
                    string printDataTempAdult = DataCountLabels.HostSaleByAccountNumAd + string.Format("[{0:D6}{1:D3}]", item.Value.TicketNO % 1000000, item.Value.AccountNO % 1000);
                    workAdultDay = TotalizeDayData.GetData(printDataTempAdult, 6);
                    workAdultTotal = TotalizeTotalData.GetData(printDataTempAdult, 6);
                    if (dataKind == DataKindType.Day)
                    {
                        sumAdult += workAdultDay;
                    }
                    else
                    {
                        sumAdult += workAdultTotal;
                    }
                    // 小児発売枚数
                    string printDataTempChild = DataCountLabels.HostSaleByAccountNumCh + string.Format("[{0:D6}{1:D3}]", item.Value.TicketNO % 1000000, item.Value.AccountNO % 1000);
                    workChildDay = TotalizeDayData.GetData(printDataTempChild, 6);
                    workChildTotal = TotalizeTotalData.GetData(printDataTempChild, 6);
                    if (dataKind == DataKindType.Day)
                    {
                        sumChild += workChildDay;
                    }
                    else
                    {
                        sumChild += workChildTotal;
                    }
                    // 回数券券種の時は、回数券発売金額をセットする
                    if (isCoupon)
                    {
                        sumCouponDay += (item.Value.AdultSale * (ulong)workAdultDay) + (item.Value.ChildSale * (ulong)workChildDay);
                        sumCouponTotal += (item.Value.AdultSale * (ulong)workAdultTotal) + (item.Value.ChildSale * (ulong)workChildTotal);
                    }

                    if (item.Value.AdultSale > 0 && item.Value.ChildSale == 0)
                    {
                        // 大人発売金額が０円でなく、小人発売金額が０円の場合は小人のエリアはブランクとする
                        printDataTempAccount = string.Format(" {0,5}／{1,6}　　　　　　　　", item.Value.AdultSale % 100000, sumAdult % 1000000);
                    }
                    else
                    {
                        printDataTempAccount = string.Format(" {0,5}／{1,6}　 {2,5}／{3,6}", item.Value.AdultSale % 100000, sumAdult % 1000000, item.Value.ChildSale % 100000, sumChild % 1000000);
                    }
                    outPut = printDataTempAccount;
                }
            }
            if (outPut != string.Empty)
            {
                // 口座別データ印字ありならば
                if (accountPrint)
                {
                    // 印字データがあれば最後の印字行を追加
                    PrintData.Add(outPut);
                }
            }
            // 空行
            {
                PrintData.Add(string.Empty);
            }
        }

        /// <summary>
        /// 取扱データ
        /// </summary>
        private void ClosingDataEditForTrade()
        {
            TroubleDataLabels troubleLabel = new TroubleDataLabels();
            troubleLabel.Initialize();
            // タイトル
            PrintData.Add("取扱データ　　　　　　　　　　");
            // 試刷
            List<string> testPrint = new List<string> { troubleLabel.LabelList[(int)TroubleData.TestTicketNum], troubleLabel.LabelList[(int)TroubleData.TestTicketSumNum] };
            PrintData.Add(string.Format("　試刷　　　　　　　　　{0,4}枚", TroubleDataItem.GetData(testPrint, 4)));
            // １０円単発枚数
            PrintData.Add(string.Format("　単発　　10円　　　　　{0,4}枚", TroubleDataItem.GetData(troubleLabel.LabelList[(int)TroubleData.SpewMoney10], 4)));
            // ５０円単発枚数
            PrintData.Add(string.Format("　単発　　50円　　　　　{0,4}枚", TroubleDataItem.GetData(troubleLabel.LabelList[(int)TroubleData.SpewMoney50], 4)));
            // １００円単発枚数
            PrintData.Add(string.Format("　単発　 100円　　　　　{0,4}枚", TroubleDataItem.GetData(troubleLabel.LabelList[(int)TroubleData.SpewMoney100], 4)));
            // ５００円単発枚数
            PrintData.Add(string.Format("　単発　 500円　　　　　{0,4}枚", TroubleDataItem.GetData(troubleLabel.LabelList[(int)TroubleData.SpewMoney500], 4)));
            // １０００円単発枚数
            PrintData.Add(string.Format("　単発　1000円　　　　　{0,4}枚", TroubleDataItem.GetData(troubleLabel.LabelList[(int)TroubleData.SpewMoney1000], 4)));
            // つり銭異常回数
            PrintData.Add(string.Format("　釣銭異常　　　　　　　{0,4}回", TroubleDataItem.GetData(troubleLabel.LabelList[(int)TroubleData.AbnormalChangeTotal], 4)));
            // 硬貨つまり回数
            PrintData.Add(string.Format("　硬貨つまり　　　　　　{0,4}回", TroubleDataItem.GetData(troubleLabel.LabelList[(int)TroubleData.JamCoin], 4)));
            // 呼出回数
            PrintData.Add(string.Format("　呼出　　　　　　　　　{0,4}回", TroubleDataItem.GetData(troubleLabel.LabelList[(int)TroubleData.CallCount], 4)));
            // 異常券枚数
            PrintData.Add(string.Format("　異常券　　　　　　　　{0,4}枚", TroubleDataItem.GetData(troubleLabel.LabelList[(int)TroubleData.AbnormalNum], 4)));
            // 廃札券枚数
            PrintData.Add(string.Format("　廃札券　　　　　　　　{0,4}枚", TroubleDataItem.GetData(troubleLabel.LabelList[(int)TroubleData.ScrapNum], 4)));
            // 単独運転回数
            PrintData.Add(string.Format("　単独運転　　　　　　　{0,4}回", TroubleDataItem.GetData(troubleLabel.LabelList[(int)TroubleData.StandAloneCount], 4)));
            // 取消（旅客）回数
            PrintData.Add(string.Format("　取消（旅客）　　　　　{0,4}回", TroubleDataItem.GetData(troubleLabel.LabelList[(int)TroubleData.PassengerCancelCount], 4)));
            // 取消（係員）回数
            PrintData.Add(string.Format("　取消（係員）　　　　　{0,4}回", TroubleDataItem.GetData(troubleLabel.LabelList[(int)TroubleData.OperatorCancelCount], 4)));
            // 空行
            PrintData.Add(string.Empty);
        }

        /// <summary>
        /// 現金編集
        /// </summary>
        private void ClosingDataEditForCash()
        {
            // 日計／累計タイトル
            PrintData.Add("　　　　　　　日計　　　累計　");
            long workDay = TotalizeDayData.GetData(DataCountLabels.TotalCashSaleAmount, 8);
            long workTotal = TotalizeTotalData.GetData(DataCountLabels.TotalCashSaleAmount, 8);
            PrintData.Add(string.Format("　現金　　{0,8}　{1,8}円", workDay, workTotal));
            // 空行
            PrintData.Add(string.Empty);
        }

        /// <summary>
        /// オレカ編集
        /// </summary>
        private void ClosingDataEditForOrangeCard()
        {
            // タイトル
            PrintData.Add("オレカ　　　　　　　　　　　　");
            // 発行機関別
            for (int i = 0; OrangeOrgan[i] != 0; i++)
            {
                string label = DataCountLabels.OrangeOrganUseAmount + string.Format("[{0:D3}]", OrangeOrgan[i]);
                long workDay = TotalizeDayData.GetData(label, 8);
                long workTotal = TotalizeTotalData.GetData(label, 8);
                PrintData.Add(string.Format("　 {0:D3}　　{1,8}　{2,8}円", OrangeOrgan[i], workDay, workTotal));
            }
            // 小計
            PrintData.Add(string.Format("　小計　　{0,8}　{1,8}円", TotalizeOrangeUseAmount(true, 8), TotalizeOrangeUseAmount(false, 8)));
            // 空行
            PrintData.Add(string.Empty);
        }

        /// <summary>
        /// イオカ編集
        /// </summary>
        private void ClosingDataEditForIoCard()
        {
            // タイトル
            PrintData.Add("イオカ　　　　　　　　　　　　");
            for (int i = 0; IoOrgan[i] != 0; i++)
            {
                string label = DataCountLabels.SfcardOrganUseAmount + string.Format("[{0:D3}]", IoOrgan[i]);
                long workDay = TotalizeDayData.GetData(label, 8);
                long workTotal = TotalizeTotalData.GetData(label, 8);
                PrintData.Add(string.Format("　 {0:D3}　　{1,8}　{2,8}円", IoOrgan[i], workDay, workTotal));
            }
            // 小計
            PrintData.Add(string.Format("　小計　　{0,8}　{1,8}円", TotalizeSfcardOrangeUseAmount(true, 8), TotalizeSfcardOrangeUseAmount(false, 8)));
            // 空行
            PrintData.Add(string.Empty);
        }

        /// <summary>
        /// Suica編集
        /// </summary>
        private void ClosingDataEditForSuica()
        {
            if (DataStore.GetInstance().MaintenanceSettings.IcCardUsable.Value)
            {
                // タイトル
                PrintData.Add("ＩＣカード（大人）　　　　　　");
                // 発行機関別（大人）
                long suicaDayAdult = 0;
                long suicaTotalAdult = 0;
                for (int i = 0; SuicaOrgan[i] != 0; i++)
                {
                    string label = DataCountLabels.IccardOrganUseAmount + string.Format("[{0:D3}]", SuicaOrgan[i]);
                    long workDay = TotalizeDayData.GetData(label, 8);
                    long workTotal = TotalizeTotalData.GetData(label, 8);
                    PrintData.Add(string.Format("　 {0:D3}　　{1,8}　{2,8}円", SuicaOrgan[i], workDay, workTotal));
                    suicaDayAdult += workDay;
                    suicaTotalAdult += workTotal;
                }
                // 小計
                PrintData.Add(string.Format("　小計　　{0,8}　{1,8}円", suicaDayAdult % 100000000, suicaTotalAdult % 100000000));
                // 空行
                PrintData.Add(string.Empty);
                // タイトル
                PrintData.Add("ＩＣカード（小児）　　　　　　");
                // 発行機関別（小児）
                long suicaDayChild = 0;
                long suicaTotalChild = 0;
                for (int i = 0; SuicaOrgan[i] != 0; i++)
                {
                    string label = DataCountLabels.IccardOrganUseAmountChild + string.Format("[{0:D3}]", SuicaOrgan[i]);
                    long workDay = TotalizeDayData.GetData(label, 8);
                    long workTotal = TotalizeTotalData.GetData(label, 8);
                    PrintData.Add(string.Format("　 {0:D3}　　{1,8}　{2,8}円", SuicaOrgan[i], workDay, workTotal));
                    suicaDayChild += workDay;
                    suicaTotalChild += workTotal;
                }
                // 小計
                PrintData.Add(string.Format("　小計　　{0,8}　{1,8}円", suicaDayChild % 100000000, suicaTotalChild % 100000000));
                // 空行
                PrintData.Add(string.Empty);
            }
        }

        /// <summary>
        /// 自社他社区分
        /// </summary>
        private void ClosingDataEditForOtherCompany()
        {
            // タイトル
            PrintData.Add("自社他社区分　　　　　　　　　");
            // 自社枚数
            long workDay = TotalizeDayData.GetData(DataCountLabels.TotalOwnSaleNum, 8);
            long workTotal = TotalizeTotalData.GetData(DataCountLabels.TotalOwnSaleNum, 8);
            PrintData.Add(string.Format("　自社　　{0,8}　{1,8}枚", workDay, workTotal));
            // 自社金額
            workDay = TotalizeDayData.GetData(DataCountLabels.TotalOwnSaleAmount, 8);
            workTotal = TotalizeTotalData.GetData(DataCountLabels.TotalOwnSaleAmount, 8);
            PrintData.Add(string.Format("　　　　　{0,8}　{1,8}円", workDay, workTotal));
            // 他社枚数
            workDay = TotalizeDayData.GetData(DataCountLabels.TotalOthSaleNum, 8);
            workTotal = TotalizeTotalData.GetData(DataCountLabels.TotalOthSaleNum, 8);
            PrintData.Add(string.Format("　他社　　{0,8}　{1,8}枚", workDay, workTotal));
            // 他社金額
            workDay = TotalizeDayData.GetData(DataCountLabels.TotalOthSaleAmount, 8);
            workTotal = TotalizeTotalData.GetData(DataCountLabels.TotalOthSaleAmount, 8);
            PrintData.Add(string.Format("　　　　　{0,8}　{1,8}円", workDay, workTotal));
            // 空行
            PrintData.Add(string.Empty);
        }

        /// <summary>
        /// 回数券
        /// </summary>
        /// <param name="sumCouponDay">回数券金額（日計）</param>
        /// <param name="sumCouponTotal">回数券金額（累計）</param>
        private void ClosingDataEditForCoupon(ulong sumCouponDay, ulong sumCouponTotal)
        {
            PrintData.Add(string.Format("回数券　　{0,8}　{1,8}円", sumCouponDay % 100000000, sumCouponTotal % 100000000));
            // 空行
            PrintData.Add(string.Empty);
        }

        /// <summary>
        /// スイカチャージ
        /// </summary>
        private void ClosingDataEditForSuicaCharge()
        {
            long workDay = 0;
            long workTotal = 0;
            if (DataStore.GetInstance().MaintenanceSettings.IcCardUsable.Value)
            {
                // タイトル
                PrintData.Add("ＩＣカードチャージ（大人）　　");
                PrintData.Add("　現金　　　　　　　　　　　　");
                // 発行機関別（大人）
                for (int i = 0; SuicaOrgan[i] != 0; i++)
                {
                    // チャージ件数
                    string label = DataCountLabels.IccardChargeNum + string.Format("[{0:D3}]", SuicaOrgan[i]);
                    workDay = TotalizeDayData.GetData(label, 8);
                    workTotal = TotalizeTotalData.GetData(label, 8);
                    PrintData.Add(string.Format("　　 {0:D3}　{1,8}　{2,8}件", SuicaOrgan[i], workDay, workTotal));
                    // チャージ金額
                    label = DataCountLabels.IccardChargeAmount + string.Format("[{0:D3}]", SuicaOrgan[i]);
                    workDay = TotalizeDayData.GetData(label, 8);
                    workTotal = TotalizeTotalData.GetData(label, 8);
                    PrintData.Add(string.Format("　　　　　{0,8}　{1,8}円", workDay, workTotal));
                }
                // 空行
                PrintData.Add(string.Empty);
                // タイトル
                PrintData.Add("ＩＣカードチャージ（小児）　　");
                PrintData.Add("　現金　　　　　　　　　　　　");
                // 発行機関別（小児）
                for (int i = 0; SuicaOrgan[i] != 0; i++)
                {
                    // チャージ件数
                    string label = DataCountLabels.IccardChargeNumChild + string.Format("[{0:D3}]", SuicaOrgan[i]);
                    workDay = TotalizeDayData.GetData(label, 8);
                    workTotal = TotalizeTotalData.GetData(label, 8);
                    PrintData.Add(string.Format("　　 {0:D3}　{1,8}　{2,8}件", SuicaOrgan[i], workDay, workTotal));
                    // チャージ金額
                    label = DataCountLabels.IccardChargeAmountChild + string.Format("[{0:D3}]", SuicaOrgan[i]);
                    workDay = TotalizeDayData.GetData(label, 8);
                    workTotal = TotalizeTotalData.GetData(label, 8);
                    PrintData.Add(string.Format("　　　　　{0,8}　{1,8}円", workDay, workTotal));
                }

                // 空行
                PrintData.Add(string.Empty);
            }
        }

        /// <summary>
        /// スイカデポジット
        /// </summary>
        private void ClosingDataEditForSuicaDeposit()
        {
            if (DataStore.GetInstance().MaintenanceSettings.IcCardUsable.Value)
            {
                // タイトル
                PrintData.Add("ＩＣカードデポジット　　　　　");
                long amountDay = 0;
                long amountTotal = 0;
                // デポジット徴収件数
                long workDay = TotalizeDayData.GetData(DataCountLabels.IccardDipositTollNum, 8);
                long workTotal = TotalizeTotalData.GetData(DataCountLabels.IccardDipositTollNum, 8);
                PrintData.Add(string.Format("　徴収　　{0,8}　{1,8}件", workDay, workTotal));
                // デポジット徴収金額
                workDay = TotalizeDayData.GetData(DataCountLabels.IccardDipositTollAmount, 8);
                workTotal = TotalizeTotalData.GetData(DataCountLabels.IccardDipositTollAmount, 8);
                PrintData.Add(string.Format("　　　　　{0,8}　{1,8}円", workDay, workTotal));
                amountDay += workDay;
                amountTotal += workTotal;
                // デポジット返却件数
                workDay = TotalizeDayData.GetData(DataCountLabels.IccardDipositRestNum, 8);
                workTotal = TotalizeTotalData.GetData(DataCountLabels.IccardDipositRestNum, 8);
                PrintData.Add(string.Format("　返却　　{0,8}　{1,8}件", workDay, workTotal));
                // デポジット返却金額
                workDay = TotalizeDayData.GetData(DataCountLabels.IccardDipositRestAmount, 8);
                workTotal = TotalizeTotalData.GetData(DataCountLabels.IccardDipositRestAmount, 8);
                PrintData.Add(string.Format("　　　　　{0,8}　{1,8}円", workDay, workTotal));
                amountDay -= workDay;
                amountTotal -= workTotal;
                // デポジット合計金額
                PrintData.Add(string.Format("　合計　　{0,8}　{1,8}円", amountDay % 100000000, amountTotal % 100000000));
                // 空行
                PrintData.Add(string.Empty);
            }
        }

        /// <summary>
        /// 総売上
        /// </summary>
        private void ClosingDataEditForTotal()
        {
            // タイトル
            PrintData.Add("総売上　　　　　　　　　　　　");
            // 現金
            PrintData.Add(string.Format("　現金　　{0,8}　{1,8}円", TotalizeDayData.GetData(DataCountLabels.TotalCashSaleAmount, 8), TotalizeTotalData.GetData(DataCountLabels.TotalCashSaleAmount, 8)));

            if (DataStore.GetInstance().MaintenanceSettings.SfCardUsable.Value)
            {
                // オレカ
                PrintData.Add(string.Format("　オレカ　{0,8}　{1,8}円", TotalizeOrangeUseAmount(true, 8), TotalizeOrangeUseAmount(false, 8)));
            }

            if (DataStore.GetInstance().MaintenanceSettings.PpCardUsable.Value)
            {
                // イオカ
                PrintData.Add(string.Format("　イオカ　{0,8}　{1,8}円", TotalizeSfcardOrangeUseAmount(true, 8), TotalizeSfcardOrangeUseAmount(false, 8)));
            }

            if (DataStore.GetInstance().MaintenanceSettings.IcCardUsable.Value)
            {
                // スイカ
                PrintData.Add(string.Format("　ＩＣ　　{0,8}　{1,8}円", TotalizeIccardUseAmount(true, 8), TotalizeIccardUseAmount(false, 8)));
            }
            // 合計
            PrintData.Add(string.Format("　合計　　{0,8}　{1,8}円", TotalizeDayData.GetData(DataCountLabels.TotalSaleAmount, 8), TotalizeTotalData.GetData(DataCountLabels.TotalSaleAmount, 8)));
            // 空行
            PrintData.Add(string.Empty);
        }

        /// <summary>
        /// スイカ移替
        /// </summary>
        private void ClosingDataEditForOrganChange()
        {
            if (DataStore.GetInstance().MaintenanceSettings.IcCardUsable.Value)
            {
                // タイトル
                PrintData.Add("ＩＣカード　　　　　　　　　　");
                // ＳＦ移替件数
                long workDay = TotalizeDayData.GetData(DataCountLabels.IccardOrganChangeNum, 8);
                long workTotal = TotalizeTotalData.GetData(DataCountLabels.IccardOrganChangeNum, 8);
                PrintData.Add(string.Format("　SF移替　{0,8}　{1,8}件", workDay, workTotal));
                // ＳＦ移替金額
                workDay = TotalizeDayData.GetData(DataCountLabels.IccardOrganChangeAmount, 8);
                workTotal = TotalizeTotalData.GetData(DataCountLabels.IccardOrganChangeAmount, 8);
                PrintData.Add(string.Format("　　　　　{0,8}　{1,8}円", workDay, workTotal));
                // 空行
                PrintData.Add(string.Empty);
            }
        }

        /// <summary>
        /// 誤購入払戻
        /// </summary>
        private void ClosingDataEditForRefund()
        {
            if (DataStore.GetInstance().MaintenanceSettings.Refund.Value)
            {
                // タイトル
                PrintData.Add("誤購入払戻　　　　　　　　　　");
                // 誤購入払戻枚数
                long workDay = TotalizeDayData.GetData(DataCountLabels.TicketRefundNum, 8);
                long workTotal = TotalizeTotalData.GetData(DataCountLabels.TicketRefundNum, 8);
                PrintData.Add(string.Format("　枚数　　{0,8}　{1,8}枚", workDay, workTotal));
                // 誤購入払戻金額
                workDay = TotalizeDayData.GetData(DataCountLabels.TicketRefundAmount, 8);
                workTotal = TotalizeTotalData.GetData(DataCountLabels.TicketRefundAmount, 8);
                PrintData.Add(string.Format("　金額　　{0,8}　{1,8}円", workDay, workTotal));
                // 空行
                PrintData.Add(string.Empty);
            }
        }

        /// <summary>
        /// ＳＦ返金：保守設定「ＳＦ返金機能」機能あり の場合のみ表示
        /// </summary>
        private void ClosingDataEditForSfBack()
        {
            if (DataStore.GetInstance().MaintenanceSettings.SfBack.Value)
            {
                // タイトル
                PrintData.Add("ＳＦ返金チャージ（大人）　　　");
                // 発行機関別（大人）
                for (int i = 0; SuicaOrgan[i] != 0; i++)
                {
                    // ＳＦ返金件数
                    string label = DataCountLabels.IccardSfbackNum + string.Format("[{0:D3}]", SuicaOrgan[i]);
                    long workDay = TotalizeDayData.GetData(label, 8);
                    long workTotal = TotalizeTotalData.GetData(label, 8);
                    PrintData.Add(string.Format("　　 {0:D3}　{1,8}　{2,8}件", SuicaOrgan[i], workDay, workTotal));
                    // ＳＦ返金金額
                    label = DataCountLabels.IccardSfbackAmount + string.Format("[{0:D3}]", SuicaOrgan[i]);
                    workDay = TotalizeDayData.GetData(label, 8);
                    workTotal = TotalizeTotalData.GetData(label, 8);
                    PrintData.Add(string.Format("　　　　　{0,8}　{1,8}円", workDay, workTotal));
                }
                // 空行
                PrintData.Add(string.Empty);
                // タイトル
                PrintData.Add("ＳＦ返金チャージ（小児）　　　");
                // 発行機関別（小児）
                for (int i = 0; SuicaOrgan[i] != 0; i++)
                {
                    // ＳＦ返金件数
                    string label = DataCountLabels.IccardSfbackNumChild + string.Format("[{0:D3}]", SuicaOrgan[i]);
                    long workDay = TotalizeDayData.GetData(label, 8);
                    long workTotal = TotalizeTotalData.GetData(label, 8);
                    PrintData.Add(string.Format("　　 {0:D3}　{1,8}　{2,8}件", SuicaOrgan[i], workDay, workTotal));
                    // ＳＦ返金金額
                    label = DataCountLabels.IccardSfbackAmountChild + string.Format("[{0:D3}]", SuicaOrgan[i]);
                    workDay = TotalizeDayData.GetData(label, 8);
                    workTotal = TotalizeTotalData.GetData(label, 8);
                    PrintData.Add(string.Format("　　　　　{0,8}　{1,8}円", workDay, workTotal));
                }
                // 空行
                PrintData.Add(string.Empty);
            }
        }

        /// <summary>
        /// ポイントチャージ：保守設定「ポイントチャージ機能」機能あり の場合のみ表示
        /// </summary>
        private void ClosingDataEditForPointCharge()
        {
            if (DataStore.GetInstance().MaintenanceSettings.PointCharge.Value)
            {
                // タイトル
                PrintData.Add("ポイントチャージ（大人）　　　");
                // 発行機関別（大人）
                for (int i = 0; SuicaOrgan[i] != 0; i++)
                {
                    // ポイントチャージ件数
                    string label = DataCountLabels.IccardPchargeNum + string.Format("[{0:D3}]", SuicaOrgan[i]);
                    long workDay = TotalizeDayData.GetData(label, 8);
                    long workTotal = TotalizeTotalData.GetData(label, 8);
                    PrintData.Add(string.Format("　　 {0:D3}　{1,8}　{2,8}件", SuicaOrgan[i], workDay, workTotal));
                    // ポイントチャージ金額
                    label = DataCountLabels.IccardPchargeAmount + string.Format("[{0:D3}]", SuicaOrgan[i]);
                    workDay = TotalizeDayData.GetData(label, 8);
                    workTotal = TotalizeTotalData.GetData(label, 8);
                    PrintData.Add(string.Format("　　　　　{0,8}　{1,8}円", workDay, workTotal));
                }
                // 空行
                PrintData.Add(string.Empty);
                // タイトル
                PrintData.Add("ポイントチャージ（小児）　　　");
                // 発行機関別（小児）
                for (int i = 0; SuicaOrgan[i] != 0; i++)
                {
                    // ポイントチャージ件数
                    string label = DataCountLabels.IccardPchargeNumChild + string.Format("[{0:D3}]", SuicaOrgan[i]);
                    long workDay = TotalizeDayData.GetData(label, 8);
                    long workTotal = TotalizeTotalData.GetData(label, 8);
                    PrintData.Add(string.Format("　　 {0:D3}　{1,8}　{2,8}件", SuicaOrgan[i], workDay, workTotal));
                    // ポイントチャージ金額
                    label = DataCountLabels.IccardPchargeAmountChild + string.Format("[{0:D3}]", SuicaOrgan[i]);
                    workDay = TotalizeDayData.GetData(label, 8);
                    workTotal = TotalizeTotalData.GetData(label, 8);
                    PrintData.Add(string.Format("　　　　　{0,8}　{1,8}円", workDay, workTotal));
                }
                // 空行
                PrintData.Add(string.Empty);
            }
        }

        /// <summary>
        /// 印字プロパティ取得（出力葉数、締切データ種別、口座別売上プリント有無）
        /// </summary>
        /// <param name="printCount">出力：葉数</param>
        /// <param name="dataKind">出力：締切データ種別</param>
        /// <param name="accountPrint">出力：口座別売上プリント有無</param>
        /// <param name="saveFilePath">出力：保存ファイルパス</param>
        private void GetPrintDataProperty(out int printCount, out DataKindType dataKind, out bool accountPrint, out string saveFilePath)
        {
            dataKind = DataKindType.Day;
            saveFilePath = string.Empty;
            // 口座別売上プリント有無
            accountPrint = false;
            // 出力葉数を求める
            printCount = 0;
            switch (operatorInfo.ClosePattern)
            {
                // RP締切（日計）
                case OperatorInfo.ClosePatternType.RpCloseDay:
                    // 締切データ種別
                    dataKind = DataKindType.Day;
                    // 保存ファイル
                    saveFilePath = DayCurDir;
                    // QR出力（日計）場合も機能仕様より口座別データ印字ある/２葉設定
                    // RP締切（累計）
                    // 口座別データ印字有無
                    accountPrint = true;
                    // 出力葉数
                    printCount = 2;
                    break;
                case OperatorInfo.ClosePatternType.RpCloseTotal:
                    // 締切データ種別
                    dataKind = DataKindType.Total;
                    // 保存ファイル
                    saveFilePath = TotalCurDir;
                    if (operatorInfo.IsQrMode)
                    {
                        // QRコード出力
                        // 口座別データ印字有無
                        accountPrint = false;
                        // 出力葉数
                        printCount = 2;
                    }
                    else
                    {
                        // RP締切（累計）
                        // 口座別データ印字有無
                        accountPrint = true;
                        // 出力葉数
                        printCount = 2;
                    }
                    break;
                default:
                    break;
            }
        }

        /// <summary>
        /// 出力データを保存する
        /// </summary>
        /// <param name="dataKind">締切データ種別</param>
        /// <param name="accountPrint">口座別売上プリント有無</param>
        /// <param name="saveFilePath">保存ファイルパス</param>
        /// <returns>True:成功</returns>
        private bool SavePrintData(DataKindType dataKind, bool accountPrint, string saveFilePath)
        {
            bool ret = false;
            // 今回分のデータを編集
            ClosingDataEdit(DataCountBase.Generation.Cur, dataKind, 0, accountPrint);
            ret = SavePrintData(saveFilePath);
            ClearPrintData();
            return ret;
        }

        /// <summary>
        /// 再出力時に未送データと今回データ間カットできるため指定行ブランクを追加する
        /// </summary>
        /// <param name="feedValue">指定行</param>
        private void AddCut(int feedValue)
        {
            for (int i = 0; i < feedValue; i++)
            {
                PrintData.Add(string.Empty);
            }
        }

        /// <summary>
        /// 印字用データを作成
        /// </summary>
        /// <param name="printCount">葉数</param>
        /// <param name="dataKind">締切データ種別</param>
        /// <param name="accountPrint">口座別売上プリント有無</param>
        /// <returns>印字回数</returns>
        private int CreatePrintData(int printCount, DataKindType dataKind, bool accountPrint)
        {
            // 未送なし
            for (int i = 0; i < printCount; i++)
            {
                var printaDataCreate = new PrintDataCreator();
                // 今回分データを編集
                printaDataCreate.ClosingDataEdit(DataCountBase.Generation.Cur, dataKind, 0, accountPrint);
                PrintDataCreatorList.Add(printaDataCreate);
            }
            return PrintDataCreatorList.Count;
        }

        /// <summary>
        /// オレカ合計金額
        /// </summary>
        /// <param name="isDay">True：日計</param>
        /// <param name="figure">最大桁数</param>
        /// <returns>合計金額</returns>
        private long TotalizeOrangeUseAmount(bool isDay, int figure)
        {
            long ret = 0;
            for (int i = 0; OrangeOrgan[i] != 0; i++)
            {
                string label = DataCountLabels.OrangeOrganUseAmount + string.Format("[{0:D3}]", OrangeOrgan[i]);
                if (isDay)
                {
                    ret += TotalizeDayData.GetData(label, 8);
                }
                else
                {
                    ret += TotalizeTotalData.GetData(label, 8);
                }
            }
            return ret %= (long)Math.Pow(10, figure);
        }

        /// <summary>
        /// イオカ合計金額
        /// </summary>
        /// <param name="isDay">True：日計</param>
        /// <param name="figure">最大桁数</param>
        /// <returns>合計金額</returns>
        private long TotalizeSfcardOrangeUseAmount(bool isDay, int figure)
        {
            long ret = 0;
            for (int i = 0; IoOrgan[i] != 0; i++)
            {
                string label = DataCountLabels.SfcardOrganUseAmount + string.Format("[{0:D3}]", IoOrgan[i]);
                if (isDay)
                {
                    ret += TotalizeDayData.GetData(label, 8);
                }
                else
                {
                    ret += TotalizeTotalData.GetData(label, 8);
                }
            }
            return ret %= (long)Math.Pow(10, figure);
        }

        /// <summary>
        /// スイカ合計金額（大人/小人）
        /// </summary>
        /// <param name="isDay">True：日計</param>
        /// <param name="figure">最大桁数</param>
        /// <returns>合計金額</returns>
        private long TotalizeIccardUseAmount(bool isDay, int figure)
        {
            long ret = 0;
            for (int i = 0; SuicaOrgan[i] != 0; i++)
            {
                string labelAdult = DataCountLabels.IccardOrganUseAmount + string.Format("[{0:D3}]", SuicaOrgan[i]);
                string labelChild = DataCountLabels.IccardOrganUseAmountChild + string.Format("[{0:D3}]", SuicaOrgan[i]);
                if (isDay)
                {
                    ret += TotalizeDayData.GetData(labelAdult, 8);
                    ret += TotalizeDayData.GetData(labelChild, 8);
                }
                else
                {
                    ret += TotalizeTotalData.GetData(labelAdult, 8);
                    ret += TotalizeTotalData.GetData(labelChild, 8);
                }
            }
            return ret %= (long)Math.Pow(10, figure);
        }
        /// <summary>
        /// きっぷ枚数／金額
        /// </summary>
        private void ClosingDataEditForTiecket()
        {
            // タイトル
            PrintData.Add("きっぷ");
            // 売上枚数
            long num = TotalizeDayData.GetData(DataCountLabels.TotalSaleNum, 6);
            PrintData.Add(string.Format("　売上枚数　　　　　　{0,6}枚", num));
            // 売上金額
            long amount = TotalizeDayData.GetData(DataCountLabels.TotalSaleAmount, 8);
            PrintData.Add(string.Format("　売上金額　　　　　{0,8}円", amount));
            // 現金売上金額
            amount = TotalizeDayData.GetData(DataCountLabels.TotalCashSaleAmount, 8);
            PrintData.Add(string.Format("　現金売上金額　　　{0,8}円", amount));
            // カード売上金額
            if (settings.PpCardUsable.Value || settings.SfCardUsable.Value)
            {
                amount = TotalizeDayData.GetData(DataCountLabels.TotalMagCardSaleAmount, 8);
                PrintData.Add(string.Format("　カード売上金額　　{0,8}円", amount));
            }
            // ＩＣカード売上金額
            if (settings.IcCardUsable.Value)
            {
                amount = TotalizeDayData.GetData(DataCountLabels.TotalIcCardSaleAmount, 8);
                PrintData.Add(string.Format("　ＩＣカード売上金額{0,8}円", amount));
            }
            // 自社完結売上枚数
            num = TotalizeDayData.GetData(DataCountLabels.TotalOwnSaleNum, 6);
            PrintData.Add(string.Format("　自社完結売上枚数　　{0,6}枚", num));
            // 自社完結売上金額
            amount = TotalizeDayData.GetData(DataCountLabels.TotalOwnSaleAmount, 8);
            PrintData.Add(string.Format("　自社完結売上金額　{0,8}円", amount));
            // 他社関連売上枚数
            num = TotalizeDayData.GetData(DataCountLabels.TotalOthSaleNum, 6);
            PrintData.Add(string.Format("　他社関連売上枚数　　{0,6}枚", num));
            // 他社関連売上金額
            amount = TotalizeDayData.GetData(DataCountLabels.TotalOthSaleAmount, 8);
            PrintData.Add(string.Format("　他社関連売上金額　{0,8}円", amount));
            // 空行
            PrintData.Add(string.Empty);

            // 異常券枚数
            num = TotalizeDayData.GetData(DataCountLabels.PurchaseAbnormalNum, 6);
            PrintData.Add(string.Format("　異常券枚数　　　　　{0,6}枚", num));
            // 廃札券枚数
            num = TotalizeDayData.GetData(DataCountLabels.PurchaseScrapNum, 6);
            PrintData.Add(string.Format("　廃札券枚数　　　　　{0,6}枚", num));
            // 誤購入払戻枚数
            if (settings.Refund.Value)
            {
                num = TotalizeDayData.GetData(DataCountLabels.TicketRefundNum, 6);
                PrintData.Add(string.Format("　誤購入払戻枚数　　　{0,6}枚", num));
                // 誤購入払戻金額
                amount = TotalizeDayData.GetData(DataCountLabels.TicketRefundAmount, 8);
                PrintData.Add(string.Format("　誤購入払戻金額　　{0,8}円", amount));
            }
        }

        /// <summary>
        /// 定期券
        /// </summary>
        private void ClosingDataEditForPass()
        {
            // タイトル
            PrintData.Add("定期券　＊＊＊＊＊＊＊＊＊＊＊");
            // 空行
            PrintData.Add(string.Empty);

            // 現金タイトル
            PrintData.Add("　現金売上");
            // 現金定期券自社完結枚数
            long num = TotalizeDayData.GetData(DataCountLabels.SeasonTotalOwnCashSaleNum, 6);
            // 現金定期券自社完結金額
            long amount = TotalizeDayData.GetData(DataCountLabels.SeasonTotalOwnCashSaleAmount, 9);
            PrintData.Add(string.Format("　　定期券自社完結　　{0,6}枚", num));
            PrintData.Add(string.Format("　　　　　　　　　 {0,9}円", amount));
            // 現金定期券他社関連枚数
            num = TotalizeDayData.GetData(DataCountLabels.SeasonTotalOtherCashSaleNum, 6);
            // 現金定期券他社関連金額
            amount = TotalizeDayData.GetData(DataCountLabels.SeasonTotalOtherCashSaleAmount, 9);
            PrintData.Add(string.Format("　　定期券他社関連　　{0,6}枚", num));
            PrintData.Add(string.Format("　　　　　　　　　 {0,9}円", amount));
            // クレジット売上
            if (settings.CreditCardUsable.Value)
            {
                // タイトル
                PrintData.Add("　クレジット売上");
                // クレジット定期券自社完結枚数
                num = TotalizeDayData.GetData(DataCountLabels.SeasonTotalOwnCreditSaleNum, 6);
                // クレジット定期券自社完結金額
                amount = TotalizeDayData.GetData(DataCountLabels.SeasonTotalOwnCreditSaleAmount, 9);
                PrintData.Add(string.Format("　　定期券自社完結　　{0,6}枚", num));
                PrintData.Add(string.Format("　　　　　　　　　 {0,9}円", amount));
                // クレジット定期券他社関連枚数
                num = TotalizeDayData.GetData(DataCountLabels.SeasonTotalOtherCreditSaleNum, 6);
                // クレジット定期券他社関連金額
                amount = TotalizeDayData.GetData(DataCountLabels.SeasonTotalOtherCreditSaleAmount, 9);
                PrintData.Add(string.Format("　　定期券他社関連　　{0,6}枚", num));
                PrintData.Add(string.Format("　　　　　　　　　 {0,9}円", amount));
            }
            // タイトル
            PrintData.Add("　現金廃札");
            // 現金廃札定期券自社完結枚数
            num = TotalizeDayData.GetData(DataCountLabels.SeasonTotalOwnCashRefundNum, 6);
            // 現金廃札定期券自社完結金額
            amount = TotalizeDayData.GetData(DataCountLabels.SeasonTotalOwnCashRefundAmount, 9);
            PrintData.Add(string.Format("　　定期券自社完結　　{0,6}枚", num));
            PrintData.Add(string.Format("　　　　　　　　　 {0,9}円", amount));
            // 現金廃札定期券他社関連枚数
            num = TotalizeDayData.GetData(DataCountLabels.SeasonTotalOtherCashRefundNum, 6);
            // 現金廃札定期券他社関連金額
            amount = TotalizeDayData.GetData(DataCountLabels.SeasonTotalOtherCashRefundAmount, 9);
            PrintData.Add(string.Format("　　定期券他社関連　　{0,6}枚", num));
            PrintData.Add(string.Format("　　　　　　　　　 {0,9}円", amount));
            // クレジット廃札
            if (settings.CreditCardUsable.Value)
            {
                // タイトル
                PrintData.Add("　クレジット廃札");
                // クレジット廃札定期券自社完結枚数
                num = TotalizeDayData.GetData(DataCountLabels.SeasonTotalOwnCreditRefundNum, 6);
                // クレジット廃札定期券自社完結金額
                amount = TotalizeDayData.GetData(DataCountLabels.SeasonTotalOwnCreditRefundAmount, 9);
                PrintData.Add(string.Format("　　定期券自社完結　　{0,6}枚", num));
                PrintData.Add(string.Format("　　　　　　　　　 {0,9}円", amount));
                // クレジット廃札定期券他社関連枚数
                num = TotalizeDayData.GetData(DataCountLabels.SeasonTotalOtherCreditRefundNum, 6);
                // クレジット廃札定期券他社関連金額
                amount = TotalizeDayData.GetData(DataCountLabels.SeasonTotalOtherCreditRefundAmount, 9);
                PrintData.Add(string.Format("　　定期券他社関連　　{0,6}枚", num));
                PrintData.Add(string.Format("　　　　　　　　　 {0,9}円", amount));
            }
            // 空行
            PrintData.Add(string.Empty);
        }

        /// <summary>
        /// チャージ
        /// </summary>
        private void ClosingDataEditForCharge()
        {
            if (settings.IcCardUsable.Value)
            {
                // タイトル
                PrintData.Add("チャージ　＊＊＊＊＊＊＊＊＊＊");
                // 空行
                PrintData.Add(string.Empty);

                // チャージ現金売上件数
                long num = TotalizeDayData.GetData(DataCountLabels.IccardTotalChargeNum, 6);
                // チャージ現金売上金額
                long amount = TotalizeDayData.GetData(DataCountLabels.IccardTotalChargeAmount, 8);
                PrintData.Add(string.Format("　現金売上　　　　　　{0,6}件", num));
                PrintData.Add(string.Format("　　　　　　　　　　{0,8}円", amount));
                // クレジット売上
                if (settings.CreditCardUsable.Value)
                {
                    // チャージクレジット売上件数
                    num = TotalizeDayData.GetData(DataCountLabels.IccardTotalCreditChargeNum, 6);
                    // チャージクレジット売上金額
                    amount = TotalizeDayData.GetData(DataCountLabels.IccardTotalCreditChargeAmount, 8);
                    PrintData.Add(string.Format("　クレジット売上　　　{0,6}件", num));
                    PrintData.Add(string.Format("　　　　　　　　　　{0,8}円", amount));
                }
                // チャージ現金廃札件数
                num = TotalizeDayData.GetData(DataCountLabels.IccardTotalChargeRefundNum, 6);
                // チャージ現金廃札金額
                amount = TotalizeDayData.GetData(DataCountLabels.IccardTotalChargeRefundAmount, 8);
                PrintData.Add(string.Format("　現金廃札　　　　　　{0,6}件", num));
                PrintData.Add(string.Format("　　　　　　　　　　{0,8}円", amount));
                // クレジット廃札
                if (settings.CreditCardUsable.Value)
                {
                    // チャージクレジット廃札件数
                    num = TotalizeDayData.GetData(DataCountLabels.IccardTotalCreditChargeRefundNum, 6);
                    // チャージクレジット廃札金額
                    amount = TotalizeDayData.GetData(DataCountLabels.IccardTotalCreditChargeRefundAmount, 8);
                    PrintData.Add(string.Format("　クレジット廃札　　　{0,6}件", num));
                    PrintData.Add(string.Format("　　　　　　　　　　{0,8}円", amount));
                }
                // 空行
                PrintData.Add(string.Empty);
            }
        }

        /// <summary>
        /// デポジット徴収
        /// </summary>
        private void ClosingDataEditForDiposit()
        {
            if (settings.IcCardUsable.Value)
            {
                // タイトル
                PrintData.Add("デポジット徴収　＊＊＊＊＊＊＊");
                // 空行
                PrintData.Add(string.Empty);

                // 現金徴収件数
                long num = TotalizeDayData.GetData(DataCountLabels.IccardDipositTollNum, 6);
                // 現金徴収金額
                long amount = TotalizeDayData.GetData(DataCountLabels.IccardDipositTollAmount, 8);
                PrintData.Add(string.Format("　現金徴収　　　　　　{0,6}件", num));
                PrintData.Add(string.Format("　　　　　　　　　　{0,8}円", amount));
                // クレジット徴収
                if (settings.CreditCardUsable.Value)
                {
                    // クレジット徴収件数
                    num = TotalizeDayData.GetData(DataCountLabels.IccardDipositCreditTollNum, 6);
                    // クレジット徴収金額
                    amount = TotalizeDayData.GetData(DataCountLabels.IccardDipositCreditTollAmount, 8);
                    PrintData.Add(string.Format("　クレジット徴収　　　{0,6}件", num));
                    PrintData.Add(string.Format("　　　　　　　　　　{0,8}円", amount));
                }
                // 現金廃札件数
                num = TotalizeDayData.GetData(DataCountLabels.IccardDipositRefundNum, 6);
                // 現金廃札金額
                amount = TotalizeDayData.GetData(DataCountLabels.IccardDipositRefundAmount, 8);
                PrintData.Add(string.Format("　現金廃札　　　　　　{0,6}件", num));
                PrintData.Add(string.Format("　　　　　　　　　　{0,8}円", amount));
                // クレジット廃札
                if (settings.CreditCardUsable.Value)
                {
                    // クレジット廃札件数
                    num = TotalizeDayData.GetData(DataCountLabels.IccardDipositCreditRefundNum, 6);
                    // クレジット廃札金額
                    amount = TotalizeDayData.GetData(DataCountLabels.IccardDipositCreditRefundAmount, 8);
                    PrintData.Add(string.Format("　クレジット廃札　　　{0,6}件", num));
                    PrintData.Add(string.Format("　　　　　　　　　　{0,8}円", amount));
                }
                // 空行
                PrintData.Add(string.Empty);
            }
        }

        /// <summary>
        /// デポジット返却
        /// </summary>
        private void ClosingDataEditForDipositRest()
        {
            if (settings.IcCardUsable.Value)
            {
                // タイトル
                PrintData.Add("デポジット返却　＊＊＊＊＊＊＊");
                // 空行
                PrintData.Add(string.Empty);

                // 現金返却件数
                long num = TotalizeDayData.GetData(DataCountLabels.IccardTotalDipositRestNum, 6);
                // 現金返却金額
                long amount = TotalizeDayData.GetData(DataCountLabels.IccardTotalDipositRestAmount, 8);
                PrintData.Add(string.Format("　現金返却　　　　　　{0,6}件", num));
                PrintData.Add(string.Format("　　　　　　　　　　{0,8}円", amount));
                // 空行
                PrintData.Add(string.Empty);
            }
        }

        /// <summary>
        /// Ｓｕｉｃａ
        /// </summary>
        private void ClosingDataEditForIC()
        {
            if (settings.IcCardUsable.Value)
            {
                // タイトル
                PrintData.Add("Ｓｕｉｃａ　＊＊＊＊＊＊＊＊＊");
                // 空行
                PrintData.Add(string.Empty);

                // ホッバ操出枚数
                long num = TotalizeDayData.GetData(DataCountLabels.SendOutCnt, 6);
                PrintData.Add(string.Format("　ホッバ操出枚数　　　{0,6}枚", num));
                // Ｓｕｉｃａ定期券
                num = TotalizeDayData.GetData(DataCountLabels.SendOutSeasonSuicaCnt, 6);
                PrintData.Add(string.Format("　　Ｓｕｉｃａ定期券　{0,6}枚", num));
                // 無記名Ｓｕｉｃａ
                num = TotalizeDayData.GetData(DataCountLabels.SendOutUnSignSuicaCnt, 6);
                PrintData.Add(string.Format("　　無記名Ｓｕｉｃａ　{0,6}枚", num));
                // 記名Ｓｕｉｃａ
                num = TotalizeDayData.GetData(DataCountLabels.SendOutSignSuicaCnt, 6);
                PrintData.Add(string.Format("　　記名Ｓｕｉｃａ　　{0,6}枚", num));
                // タイトル
                PrintData.Add("　交換枚数");
                // 磁気→スイカ
                num = TotalizeDayData.GetData(DataCountLabels.ChangeSeasonToSuicaCnt, 6);
                PrintData.Add(string.Format("　　　磁気　→スイカ　{0,6}枚", num));
                // スイカ→スイカ
                num = TotalizeDayData.GetData(DataCountLabels.ChangeSuicaToSuicaCnt, 6);
                PrintData.Add(string.Format("　　スイカ　→スイカ　{0,6}枚", num));
                // スイカ→ビュー
                num = TotalizeDayData.GetData(DataCountLabels.ChangeSuicaToViewCnt, 6);
                PrintData.Add(string.Format("　　スイカ　→ビュー　{0,6}枚", num));
                // ビュー→ビュー
                num = TotalizeDayData.GetData(DataCountLabels.ViewSuicaToViewSuicaExchangeNum, 6);
                PrintData.Add(string.Format("　　ビュー　→ビュー　{0,6}枚", num));
                // 空行
                PrintData.Add(string.Empty);
            }
        }

        /// <summary>
        /// 回収
        /// </summary>
        private void ClosingDataEditForCollect()
        {
            long num;
            // タイトル
            PrintData.Add("回収枚数　＊＊＊＊＊＊＊＊＊＊");
            // 空行
            PrintData.Add(string.Empty);
            if (settings.IcCardUsable.Value)
            {
                // Ｓｕｉｃａイオ（券種３）枚数
                num = TotalizeDayData.GetData(DataCountLabels.CollectSuicaIoCnt, 6);
                PrintData.Add(string.Format("　Ｓｕｉｃａイオ（券種３）"));
                PrintData.Add(string.Format("　　　　　　　　　　　{0,6}枚", num));
                // Ｓｕｉｃａカード（券種０）枚数
                num = TotalizeDayData.GetData(DataCountLabels.CollectSuicaCnt, 6);
                PrintData.Add(string.Format("　Ｓｕｉｃａカード（券種０）"));
                PrintData.Add(string.Format("　　　　　　　　　　　{0,6}枚", num));
            }
            // 磁気定期券枚数
            num = TotalizeDayData.GetData(DataCountLabels.CollectSeasonCnt, 6);
            PrintData.Add(string.Format("　磁気定期券"));
            PrintData.Add(string.Format("　　　　　　　　　　　{0,6}枚", num));
            // 空行
            PrintData.Add(string.Empty);
        }

        /// <summary>
        /// 試刷
        /// </summary>
        private void ClosingDataEditForTest()
        {
            // タイトル
            PrintData.Add("試刷　＊＊＊＊＊＊＊＊＊＊＊＊");
            // 空行
            PrintData.Add(string.Empty);
            // きっぷ枚数
            long num = TotalizeDayData.GetData(DataCountLabels.TicketTestprintNum, 6);
            PrintData.Add(string.Format("　きっぷ　　　　　　　{0,6}枚", num));
            // 磁気定期券枚数
            num = TotalizeDayData.GetData(DataCountLabels.SeasonTestprintNum, 6);
            PrintData.Add(string.Format("　磁気定期券　　　　　{0,6}枚", num));
            // 空行
            PrintData.Add(string.Empty);
        }

        /// <summary>
        /// 金庫枚数
        /// </summary>
        private void ClosingDataEditForCoffer()
        {
            // タイトル
            PrintData.Add("金庫枚数　＊＊＊＊＊＊＊＊＊＊");
            // 空行
            PrintData.Add(string.Empty);

            // １０円
            long num = TotalizeDayData.GetData(DataCountLabels.Coffer10Num, 6);
            PrintData.Add(string.Format("　　　１０円　　　　　{0,6}枚", num));
            // ５０円
            num = TotalizeDayData.GetData(DataCountLabels.Coffer50Num, 6);
            PrintData.Add(string.Format("　　　５０円　　　　　{0,6}枚", num));
            // １００円
            num = TotalizeDayData.GetData(DataCountLabels.Coffer100Num, 6);
            PrintData.Add(string.Format("　　１００円　　　　　{0,6}枚", num));
            // ５００円
            num = TotalizeDayData.GetData(DataCountLabels.Coffer500Num, 6);
            PrintData.Add(string.Format("　　５００円　　　　　{0,6}枚", num));
            // １０００円
            num = TotalizeDayData.GetData(DataCountLabels.Coffer1000Num, 6);
            PrintData.Add(string.Format("　１０００円　　　　　{0,6}枚", num));
            // ２０００円
            num = TotalizeDayData.GetData(DataCountLabels.Coffer2000Num, 6);
            PrintData.Add(string.Format("　２０００円　　　　　{0,6}枚", num));
            // ５０００円
            num = TotalizeDayData.GetData(DataCountLabels.Coffer5000Num, 6);
            PrintData.Add(string.Format("　５０００円　　　　　{0,6}枚", num));
            // １００００円
            num = TotalizeDayData.GetData(DataCountLabels.Coffer10000Num, 6);
            PrintData.Add(string.Format("１００００円　　　　　{0,6}枚", num));
            // 空行
            PrintData.Add(string.Empty);
        }

        /// <summary>
        /// 単発枚数
        /// </summary>
        private void ClosingDataEditForSingle()
        {
            // タイトル
            PrintData.Add("単発枚数　＊＊＊＊＊＊＊＊＊＊");
            // 空行
            PrintData.Add(string.Empty);

            // １０円
            long num = TotalizeDayData.GetData(DataCountLabels.TotalSingle10Num, 6);
            PrintData.Add(string.Format("　　　１０円　　　　　{0,6}枚", num));
            // ５０円
            num = TotalizeDayData.GetData(DataCountLabels.TotalSingle50Num, 6);
            PrintData.Add(string.Format("　　　５０円　　　　　{0,6}枚", num));
            // １００円
            num = TotalizeDayData.GetData(DataCountLabels.TotalSingle100Num, 6);
            PrintData.Add(string.Format("　　１００円　　　　　{0,6}枚", num));
            // ５００円
            num = TotalizeDayData.GetData(DataCountLabels.TotalSingle500Num, 6);
            PrintData.Add(string.Format("　　５００円　　　　　{0,6}枚", num));
            // １０００円
            num = TotalizeDayData.GetData(DataCountLabels.TotalSingle1000Num, 6);
            PrintData.Add(string.Format("　１０００円　　　　　{0,6}枚", num));
            // 空行
            PrintData.Add(string.Empty);
        }

        /// <summary>
        /// 取忘
        /// </summary>
        private void ClosingDataEditForForget()
        {
            // タイトル
            PrintData.Add("取忘　＊＊＊＊＊＊＊＊＊＊＊＊");
            // 空行
            PrintData.Add(string.Empty);

            // 乗車券
            long num = TotalizeDayData.GetData(DataCountLabels.ForgetTicketCnt, 6);
            PrintData.Add(string.Format("　乗車券　　　　　　　{0,6}枚", num));
            // 領収証
            num = TotalizeDayData.GetData(DataCountLabels.ForgetReceiptCnt, 6);
            PrintData.Add(string.Format("　領収証　　　　　　　{0,6}枚", num));
            if (settings.HistoryPrint.Value)
            {
                // 履歴
                num = TotalizeDayData.GetData(DataCountLabels.ForgetHistoryCnt, 6);
                PrintData.Add(string.Format("　履歴　　　　　　　　{0,6}枚", num));
            }
            if (settings.PpCardUsable.Value || settings.SfCardUsable.Value)
            {
                // カード
                num = TotalizeDayData.GetData(DataCountLabels.ForgetCardCnt, 6);
                PrintData.Add(string.Format("　カード　　　　　　　{0,6}枚", num));
            }
            // 磁気定期券
            num = TotalizeDayData.GetData(DataCountLabels.ForgetSeasonCnt, 6);
            PrintData.Add(string.Format("　磁気定期券　　　　　{0,6}枚", num));
            if (settings.IcCardUsable.Value)
            {
                // ＩＣＳＦ
                num = TotalizeDayData.GetData(DataCountLabels.ForgetIoSuicaCnt, 6);
                PrintData.Add(string.Format("　ＩＣＳＦ　　　　　　{0,6}枚", num));
                // ＩＣ定期券
                num = TotalizeDayData.GetData(DataCountLabels.ForgetSeasonSuicaCnt, 6);
                PrintData.Add(string.Format("　ＩＣ定期券　　　　　{0,6}枚", num));
            }
            // 紙幣　千円
            num = TotalizeDayData.GetData(DataCountLabels.ForgetBill1000Cnt, 6);
            PrintData.Add(string.Format("　紙幣　千円　　　　　{0,6}枚", num));
            // 紙幣　２千円
            num = TotalizeDayData.GetData(DataCountLabels.ForgetBill2000Cnt, 6);
            PrintData.Add(string.Format("　　　２千円　　　　　{0,6}枚", num));
            // 紙幣　５千円
            num = TotalizeDayData.GetData(DataCountLabels.ForgetBill5000Cnt, 6);
            PrintData.Add(string.Format("　　　５千円　　　　　{0,6}枚", num));
            if (settings.IsNoUseCertificationEnable(System.DateTime.Now))
            {
                // 未使用証
                num = TotalizeDayData.GetData(DataCountLabels.ForgetCertificationCnt, 6);
                PrintData.Add(string.Format("　未使用証　　　　　　{0,6}枚", num));
            }
            if (settings.CreditCardUsable.Value)
            {
                // 利用明細
                num = TotalizeDayData.GetData(DataCountLabels.ForgetCreditJournal, 6);
                PrintData.Add(string.Format("　ご利用明細　　　　　{0,6}枚", num));
            }
            // 空行
            PrintData.Add(string.Empty);
        }

        /// <summary>
        /// ＳＦ返金
        /// </summary>
        private void ClosingDataEditForSfback()
        {
            if (settings.CreditCardUsable.Value &&
                settings.SfBack.Value)
            {
                // タイトル
                PrintData.Add("ＳＦ返金　＊＊＊＊＊＊＊＊＊＊＊＊");
                // 空行
                PrintData.Add(string.Empty);
                // ＳＦ返金件数
                long num = TotalizeDayData.GetData(DataCountLabels.IccardTotalSfbackNum, 6);
                PrintData.Add(string.Format("　返金件数　　　　　　{0,6}件", num));
                // ＳＦ返金金額
                long amount = TotalizeDayData.GetData(DataCountLabels.IccardTotalSfbackAmount, 8);
                PrintData.Add(string.Format("　返金金額　　　　　{0,8}円", amount));
                // 空行
                PrintData.Add(string.Empty);
            }
        }

        /// <summary>
        /// ポイントチャージ
        /// </summary>
        private void ClosingDataEditForPoint()
        {
            if (settings.CreditCardUsable.Value &&
                settings.PointCharge.Value)
            {
                // タイトル
                PrintData.Add("ポイントチャージ　＊＊＊＊＊＊");
                // 空行
                PrintData.Add(string.Empty);
                // ポイントチャージ件数
                long num = TotalizeDayData.GetData(DataCountLabels.IccardTotalPchargeNum, 6);
                PrintData.Add(string.Format("　件数　　　　　　　　{0,6}件", num));
                // ポイントチャージ金額
                long amount = TotalizeDayData.GetData(DataCountLabels.IccardTotalPchargeAmount, 8);
                PrintData.Add(string.Format("　金額　　　　　　　{0,8}円", amount));
                // 空行
                PrintData.Add(string.Empty);
                // 空行
                PrintData.Add(string.Empty);
            }
        }
        #endregion

        #region 内部メソッド(QRコード編集)
        /// <summary>
        /// QRコード作成
        /// </summary>
        /// <param name="kind">締切種別</param>
        /// <param name="fileName">ファイル名</param>
        /// <returns>QRコード結果</returns>
        private OperatorInfo.QrResultType CreateQrData(DataKindType kind, string fileName)
        {
            // プリンタデータ初期化
            ClearPrintData();
            OperatorInfo.QrResultType ret = OperatorInfo.QrResultType.QrSuccess;
            try
            {
                if (fileName == string.Empty)
                {
                    // エラー
                    ret = OperatorInfo.QrResultType.QrFailure;
                }
                // 集計データ取得
                switch (kind)
                {
                    case DataKindType.Day:
                        TotalizeData = new TotalizeDataSearch(TotalizeDataCount.GetInstance().GetData(DataCountBase.Generation.Cur, TotalizeDataCount.TotalizeKind.Day, DataCountLabels.CategoryDataCount));
                        break;
                    case DataKindType.Total:
                        TotalizeData = new TotalizeDataSearch(TotalizeDataCount.GetInstance().GetData(DataCountBase.Generation.Cur, TotalizeDataCount.TotalizeKind.Total, DataCountLabels.CategoryDataCount));
                        break;
                    default:
                        // エラー
                        ret = OperatorInfo.QrResultType.QrFailure;
                        break;
                }
                //-- 共通ヘッダ部編集
                EditQrDataHeader(kind);
                //-- 口座売上データ編集
                EditSaleAccount(TotalizeData);
                //-- カードデータ編集
                EditCard(TotalizeData);
                //-- ファイル出力
                FileOutPut(fileName.Remove(fileName.Length - 4));
            }
            catch
            {
                ret = OperatorInfo.QrResultType.QrFailure;
            }
            return ret;
        }

        /// <summary>
        /// 共通ヘッダ部編集
        /// </summary>
        /// <param name="kind">締切種別</param>
        private void EditQrDataHeader(DataKindType kind)
        {
            CommonHeader = string.Empty;
            // クリア回数(締切回数)
            long clearCount = 0;
            // クリア回数
            switch (kind)
            {
                case DataKindType.Day:
                    clearCount = DataStore.GetInstance().ClosingNumber.GetClosingNumber((int)Module.UI.Model.Collections.CommonSettings.ClosingNumberType.Day);
                    break;
                case DataKindType.Total:
                    clearCount = DataStore.GetInstance().ClosingNumber.GetClosingNumber((int)Module.UI.Model.Collections.CommonSettings.ClosingNumberType.Seazon);
                    break;
                default:
                    break;
            }
            DateTime date = System.DateTime.Now;
            // 駅コード（７桁０サプレス）
            // 号機番号（３桁０サプレス）
            // ブランク３桁
            // 日付・年（２桁０サプレス）
            // 日付・月（２桁０サプレス）
            // ブランク１桁
            // 日付・日（２桁０サプレス）
            // クリア回数（３桁０サプレス）
            CommonHeader = string.Format(
                "{0:D7}{1:D3}   {2:D2}{3:D2} {4:D2}{5:D3}",
                DataStore.GetInstance().MaintenanceSettings.QrClosingCode.Value,
                DataStore.GetInstance().MaintenanceSettings.PrintNo.Value,
                date.Year % 100,
                date.Month,
                date.Day,
                clearCount % 1000);
        }

        /// <summary>
        /// 口座売上データ編集
        /// </summary>
        /// <param name="data">集計データ</param>
        private void EditSaleAccount(TotalizeDataSearch data)
        {
            EditFareData editFareData = new EditFareData();
            IOrderedEnumerable<KeyValuePair<ulong, EditFareData.EditFare>> mapEditFareOrder = null;
            Dictionary<ulong, bool> mapSaleAccount = new Dictionary<ulong, bool>();

            if (!editFareData.GetSaleAccountData(GenerationType.Current, ref mapSaleAccount))
            {
                return;
            }
            if (!editFareData.GetSortedFare(GenerationType.Current, EditFareData.FareSortType.EditQrData, ref mapEditFareOrder))
            {
                return;
            }

            ulong OldCode = 0xFFFFFFFF;
            ulong OldOrder = 0xFFFFFFFF;
            // 口座売上枚数（大小別）,大人トータル料金,売上合計金額
            long AdultNum  = 0;
            long ChildNum  = 0;
            long AdultSale = 0;
            long ChildSale = 0;
            long TotalAmount  = 0;
            foreach (var item in mapEditFareOrder)
            {
                ulong code = (item.Value.TicketNO * 1000) + item.Value.AccountNO;
                // 該当パターンでは発売されていない口座？
                bool result = false;
                if (!mapSaleAccount.TryGetValue(code, out result))
                {
                    continue;
                }
                else
                {
                    if (!result)
                    {
                        continue;
                    }
                }
                // プリント順序または印字券種コードに変化あり？
                if (OldOrder != item.Value.PrintOrder || OldCode != item.Value.PrintCode)
                {
                    if (OldOrder != 0xFFFFFFFF)
                    {
                        PrintData.Add(string.Format("{0}{1,53}{2:D7}{3,5}{4:D7}{5:D6}{6,14}{7:D9}    ", CommonHeader, string.Empty.PadRight(53, ' '), AdultSale, string.Empty.PadRight(5, ' '), AdultNum, ChildNum, string.Empty.PadRight(14, ' '), TotalAmount));
                    }
                    AdultSale = (long)item.Value.AdultSale;
                    ChildSale = (long)item.Value.ChildSale;
                    AdultNum = 0;
                    ChildNum = 0;
                    TotalAmount = 0;
                    OldOrder = item.Value.PrintOrder;
                    OldCode = item.Value.PrintCode;
                }
                long adultNumTemp = 0;
                long childNumTemp = 0;
                long totalAmountTemp = 0;
                // 大人売上枚数
                adultNumTemp = data.GetData(DataCountLabels.HostSaleByAccountNumAd + string.Format("[{0:D6}{1:D3}]", item.Value.TicketNO, item.Value.AccountNO), 7);
                AdultNum += adultNumTemp;
                // 小児売上枚数
                childNumTemp = data.GetData(DataCountLabels.HostSaleByAccountNumCh + string.Format("[{0:D6}{1:D3}]", item.Value.TicketNO, item.Value.AccountNO), 6);
                ChildNum += childNumTemp;
                // 売上合計金額
                totalAmountTemp = ((long)item.Value.AdultSale * adultNumTemp) + ((long)item.Value.ChildSale * childNumTemp);
                TotalAmount += totalAmountTemp;
            }
            if (OldOrder != 0xFFFFFFFF)
            {
                PrintData.Add(string.Format("{0}{1,53}{2:D7}{3,5}{4:D7}{5:D6}{6,14}{7:D9}    ", CommonHeader, string.Empty.PadRight(53, ' '), AdultSale, string.Empty.PadRight(5, ' '), AdultNum, ChildNum, string.Empty.PadRight(14, ' '), TotalAmount));
            }
        }

        /// <summary>
        /// カードデータ編集
        /// </summary>
        /// <param name="data">集計データ</param>
        private void EditCard(TotalizeDataSearch data)
        {
            // オレカ発行機関別
            // 1:国鉄, 2:JR北海道, 3:JR東日本, 4:JR東海, 5:JR西日本, 6:JR四国, 7:JR九州
            // 発行機関別
            for (int i = 0; OrangeOrgan[i] != 0; i++)
            {
                long amount = data.GetData(DataCountLabels.OrangeOrganUseAmount + string.Format("[{0:D3}]", OrangeOrgan[i]), 9);
                PrintData.Add(string.Format("{0}{1,91}{2,1}{3:D9}{4,1}{5:D3}", CommonHeader, string.Empty.PadRight(91, ' '), (int)CARD_KIND.ORANGE, amount, (int)MONEY_OR_CHARGE.MONEY, OrangeOrgan[i]));
            }
            // 13:JR東日本
            for (int i = 0; IoOrgan[i] != 0; i++)
            {
                long amount = data.GetData(DataCountLabels.SfcardOrganUseAmount + string.Format("[{0:D3}]", IoOrgan[i]), 9);
                PrintData.Add(string.Format("{0}{1,91}{2,1}{3:D9}{4,1}{5:D3}", CommonHeader, string.Empty.PadRight(91, ' '), (int)CARD_KIND.MAGNETIC_IO, amount, (int)MONEY_OR_CHARGE.MONEY, IoOrgan[i]));
            }
            // ＩＣ使用可ならば
            if (DataStore.GetInstance().MaintenanceSettings.IcCardUsable.Value)
            {
                // 23:JR東日本,25:JR西日本,125:スルッとKANSAI,123:PB,24:JR東海,22:JR北海道,27:JR九州,127:西日本鉄道,227:福岡市交通局,124:トランパスIC
                //-- 発行機関別売上金額（合計）
                for (int i = 0; SuicaOrgan[i] != 0; i++)
                {
                    // 大人金額
                    long adultAmount = data.GetData(DataCountLabels.IccardOrganUseAmount + string.Format("[{0:D3}]", SuicaOrgan[i]), 9);
                    // 小人金額
                    long childAmount = data.GetData(DataCountLabels.IccardOrganUseAmountChild + string.Format("[{0:D3}]", SuicaOrgan[i]), 9);
                    PrintData.Add(string.Format("{0}{1,91}{2,1}{3:D9}{4,1}{5:D3}", CommonHeader, string.Empty.PadRight(91, ' '), (int)CARD_KIND.IC_CARD, adultAmount + childAmount, (int)MONEY_OR_CHARGE.MONEY, SuicaOrgan[i]));
                }
                //-- 発行機関別ＩＣチャージ金額（合計）
                for (int i = 0; SuicaOrgan[i] != 0; i++)
                {
                    // 大人枚数
                    long adultChargeNum = data.GetData(DataCountLabels.IccardChargeNum + string.Format("[{0:D3}]", SuicaOrgan[i]), 7);
                    // 大人金額
                    long adultChargeAmount = data.GetData(DataCountLabels.IccardChargeAmount + string.Format("[{0:D3}]", SuicaOrgan[i]), 9);
                    // 小人枚数
                    long childChargeNum = data.GetData(DataCountLabels.IccardChargeNumChild + string.Format("[{0:D3}]", SuicaOrgan[i]), 7);
                    // 小人金額
                    long childChargeAmount = data.GetData(DataCountLabels.IccardChargeAmountChild + string.Format("[{0:D3}]", SuicaOrgan[i]), 9);
                    PrintData.Add(string.Format("{0}{1,65}{2:D7}{3,19}{4,1}{5:D9}{6,1}{7:D3}", CommonHeader, string.Empty.PadRight(65, ' '), adultChargeNum + childChargeNum, string.Empty.PadRight(19, ' '), (int)CARD_KIND.IC_CARD, adultChargeAmount + childChargeAmount, (int)MONEY_OR_CHARGE.CHARGE, SuicaOrgan[i]));
                }
                //-- 発行機関別ＩＣ売上金額（大小別）
                for (int i = 0; SuicaOrgan[i] != 0; i++)
                {
                    // 大人金額
                    long adultOrganUseAmount = data.GetData(DataCountLabels.IccardOrganUseAmount + string.Format("[{0:D3}]", SuicaOrgan[i]), 9);
                    // 小人金額
                    long childOrganUseAmount = data.GetData(DataCountLabels.IccardOrganUseAmountChild + string.Format("[{0:D3}]", SuicaOrgan[i]), 9);
                    PrintData.Add(string.Format("{0}{1,72}{2,1}{3,18}{4,1}{5:D9}{6,1}{7:D3}", CommonHeader, string.Empty.PadRight(72, ' '), (int)ADULT_OR_CHIKD.ADULT, string.Empty.PadRight(18, ' '), (int)CARD_KIND.IC_CARD, adultOrganUseAmount, (int)MONEY_OR_CHARGE.MONEY, SuicaOrgan[i]));
                    PrintData.Add(string.Format("{0}{1,72}{2,1}{3,18}{4,1}{5:D9}{6,1}{7:D3}", CommonHeader, string.Empty.PadRight(72, ' '), (int)ADULT_OR_CHIKD.CHILD, string.Empty.PadRight(18, ' '), (int)CARD_KIND.IC_CARD, childOrganUseAmount, (int)MONEY_OR_CHARGE.MONEY, SuicaOrgan[i]));
                }
                //-- 発行機関別ＩＣチャージ売上金額（大小別）
                for (int i = 0; SuicaOrgan[i] != 0; i++)
                {
                    // 大人枚数
                    long adultChargeNum = data.GetData(DataCountLabels.IccardChargeNum + string.Format("[{0:D3}]", SuicaOrgan[i]), 7);
                    // 大人金額
                    long adultChargeAmount = data.GetData(DataCountLabels.IccardChargeAmount + string.Format("[{0:D3}]", SuicaOrgan[i]), 9);
                    // 小人枚数
                    long childChargeNum = data.GetData(DataCountLabels.IccardChargeNumChild + string.Format("[{0:D3}]", SuicaOrgan[i]), 7);
                    // 小人金額
                    long childChargeAmount = data.GetData(DataCountLabels.IccardChargeAmountChild + string.Format("[{0:D3}]", SuicaOrgan[i]), 9);
                    PrintData.Add(string.Format("{0}{1,65}{2:D7}{3,1}{4,18}{5,1}{6:D9}{7,1}{8:D3}", CommonHeader, string.Empty.PadRight(65, ' '), adultChargeNum, (int)ADULT_OR_CHIKD.ADULT, string.Empty.PadRight(18, ' '), (int)CARD_KIND.IC_CARD, adultChargeAmount, (int)MONEY_OR_CHARGE.CHARGE, SuicaOrgan[i]));
                    PrintData.Add(string.Format("{0}{1,65}{2:D7}{3,1}{4,18}{5,1}{6:D9}{7,1}{8:D3}", CommonHeader, string.Empty.PadRight(65, ' '), childChargeNum, (int)ADULT_OR_CHIKD.CHILD, string.Empty.PadRight(18, ' '), (int)CARD_KIND.IC_CARD, childChargeAmount, (int)MONEY_OR_CHARGE.CHARGE, SuicaOrgan[i]));
                }
            }
        }

        /// <summary>
        /// QRコードファイル出力
        /// </summary>
        /// <param name="fileName">ファイル名</param>
        private void FileOutPut(string fileName)
        {
            string path = Path.Combine(QrDir, fileName);
            if (!Directory.Exists(QrDir))
            {
                Directory.CreateDirectory(QrDir);
            }
            using (FileStream file = new FileStream(path, FileMode.Create, FileAccess.Write))
            {
                using (StreamWriter writer = new StreamWriter(file, Encoding.GetEncoding("shift-jis")))
                {
                    foreach (var data in PrintData)
                    {
                        writer.WriteLine(data);
                        writer.Flush();
                    }
                }
            }
            ClearPrintData();
        }
        #endregion

        #region 内部メソッド(ジャーナル編集)
        /// <summary>
        /// ジャーナル印字出力:集計データ
        /// </summary>
        /// <param name="kind">印字種別</param>
        public void JPPrintCreateCloseData(DataCountBase.JPPrintKind kind)
        {
            // ジャーナルデータ編集
            PrintData.Clear();
            // JP印字の編集処理
            switch (kind)
            {
                case DataCountBase.JPPrintKind.StartRecord:
                    // 開始記録
                    JPPrintCreateDataForStartRecord();
                    break;
                case DataCountBase.JPPrintKind.IssueRecord:
                    // 発売記録
                    JPPrintCreateDataForIssueRecord();
                    break;
                case DataCountBase.JPPrintKind.ScrapRecord:
                    // 廃札記録
                    JPPrintCreateDataForScrapRecord();
                    break;
                case DataCountBase.JPPrintKind.ExchangeRecord:
                    // 交換記録
                    JPPrintCreateDataForExchangeRecord();
                    break;
                case DataCountBase.JPPrintKind.ReissueRecord:
                    // 再発記録
                    JPPrintCreateDataForReissueRecord();
                    break;
                case DataCountBase.JPPrintKind.ClosingRecord:
                    // 締切記録
                    JPPrintCreateDataForClosingRecord();
                    break;
                case DataCountBase.JPPrintKind.ErrorRecord:
                    // 印刷開始前発券異常記録
                    JPPrintCreateDataForErrorRecord();
                    break;
                case DataCountBase.JPPrintKind.Start:
                    // 処理開始データ
                    JPPrintCreateDataForStart();
                    break;
                case DataCountBase.JPPrintKind.Test:
                    // テスト印字
                    // JPPrintCreateDataForTest();
                    break;
            }
        }

        /// <summary>
        /// ジャーナル印字出力:Ｖｉｅｗホストデータ
        /// </summary>
        /// <param name="request">Viewホスト通信用要求電文</param>
        /// <param name="response">Viewホスト通信用結果電文</param>
        /// <param name="isresult">結果電文フラグ</param>
        /// <param name="isOK">要求結果OK/NG(True:OK、False:NG)</param>
        public void JPPrintCreateViewData(object request, object response, bool isresult, bool isOK)
        {
            // ジャーナルデータ編集
            PrintData.Clear();
            // JP印字の編集処理
            if (request != null)
            {
                if (request.GetType() == typeof(MessageOpenRequest))
                {
                    // 開局処理
                    JPPrintCreateDataForOpen(request, response, isresult, isOK);
                }
                else if (request.GetType() == typeof(MessageSaleRequest))
                {
                    // 売上要求
                    JPPrintCreateDataForSale(request, response, isresult, isOK);
                }
                else if (request.GetType() == typeof(MessageSaleReturnRequest))
                {
                    // 売上戻し
                    JPPrintCreateDataForRefundResult(request, response, isresult, isOK);
                }
                else if (request.GetType() == typeof(MessageDeductionRequest))
                {
                    // 内部控除
                    JPPrintCreateDataForDeduct(request, response, isresult, isOK);
                }
                else if (request.GetType() == typeof(MessageCountRequest))
                {
                    // 精算処理
                    JPPrintCreateDataForAdjust(request, response, isresult, isOK);
                }
            }
            else
            {
                // 入力値が間違い
            }
        }

        /// <summary>
        /// ジャーナル出力機能：開始記録の作成
        /// </summary>
        private void JPPrintCreateDataForStartRecord()
        {
            // 開始記録データの編集処理
            JPDataEditForStartTitle();
            // ヘッダ部編集
            var freeFormatInput = new JpFreeFormatInput();
            freeFormatInput.PreFeed = 0;
            freeFormatInput.CutKind = JpFreeFormatInput.PaperCutKindType.NON_CUT;
            freeFormatInput.RawMode = false;
            freeFormatInput.PostFeed = 0;
            freeFormatInput.PrintInfo = PrintData.ToArray();
            var JpMediaInput = new MediaDataInput();
            JpMediaInput.EditKind = MediaDataInput.MediaEditKind.EditJpFreeFormat;
            JpMediaInput.Info = freeFormatInput.GetBytes();
            var closeJournalData = new JournalHistory.OtherJournalData();
            closeJournalData.PrintData = JpMediaInput.GetBytes();
            closeJournalData.HeadData.PrintDateTime = System.DateTime.Now;
            DataStore.GetInstance().JournalHistory.Write((object)closeJournalData);
        }

        /// <summary>
        /// ジャーナル出力機能：発売記録の作成
        /// </summary>
        private void JPPrintCreateDataForIssueRecord()
        {
            // 取引データ取得処理
            var time = session.StartTime.Value.Value;
            string oldno = string.Empty;
            string newno = string.Empty;
            bool numflag = false;
            long number = 0;

            // タイトル
            PrintData.Add(string.Format("発売　　　　取引日時　{0:D4}年{1:D2}月{2:D2}日{3:D2}時{4:D2}分{5:D2}秒", time.Year, time.Month, time.Day, time.Hour, time.Minute, time.Second));

            if (session.Operation.Value == Session.OperationType.Pass)
            {
                // 定期券発行
                numflag = EditofPass(ref newno, ref oldno, ref number);
            }
            else if ((session.Operation.Value == Session.OperationType.SignIc) ||
                     (session.Operation.Value == Session.OperationType.UnsignIc))
            {
                // 記名発行、または無記名発行
                EditofCardIssue(ref newno);
            }

            // ヘッダ部編集
            var freeFormatInput = new JpFreeFormatInput();
            freeFormatInput.PreFeed = 0;
            freeFormatInput.CutKind = JpFreeFormatInput.PaperCutKindType.NON_CUT;
            freeFormatInput.RawMode = false;
            freeFormatInput.PostFeed = 0;
            freeFormatInput.PrintInfo = PrintData.ToArray();
            var JpMediaInput = new MediaDataInput();
            JpMediaInput.EditKind = MediaDataInput.MediaEditKind.EditJpFreeFormat;
            JpMediaInput.Info = freeFormatInput.GetBytes();
            var issueJournalData = new JournalHistory.IssueJournalData();
            issueJournalData.PrintData = JpMediaInput.GetBytes();
            issueJournalData.HeadData.PrintDateTime = time;
            issueJournalData.HeadData.TicketNoSet = numflag;
            if (numflag)
            {
                issueJournalData.HeadData.TicketNo = (uint)number;
            }
            if (string.IsNullOrEmpty(oldno))
            {
                // 旧カードのIDiがブランク
                issueJournalData.HeadData.OldIDiSet = false;
            }
            else
            {
                issueJournalData.HeadData.OldIDiSet = true;
                issueJournalData.HeadData.OldIDi = Strings.StrConv(oldno, VbStrConv.Narrow);
            }
            if (string.IsNullOrEmpty(newno))
            {
                // 新カードのIDiがブランク
                issueJournalData.HeadData.IDiSet = false;
            }
            else
            {
                issueJournalData.HeadData.IDiSet = true;
                issueJournalData.HeadData.IDi = Strings.StrConv(newno, VbStrConv.Narrow);
            }

            DataStore.GetInstance().JournalHistory.Write((object)issueJournalData);
        }

        /// <summary>
        /// ＩＣカード発行発売記録の編集
        /// </summary>
        /// <param name="idi">検索用Idi</param>
        private void EditofCardIssue(ref string idi)
        {
            string temp = string.Empty;
            // 支払方法
            JPDataEditForPayment();
            // 種別、購入金額
            JPDataEditForCardIssueLine3();
            // 媒体、チャージ金額
            JPDataEditForCardIssueLine4();
            // 券種、デポジット
            JPDataEditForPassLine6();
            // 大小、投入金額
            JPDataEditForPassLine7();
            // 割引、おつり
            JPDataEditForPassLine8();
            // IDi
            JPDataEditForIDi(ref idi, ref temp);
            // 個人データ
            JPDataEditForPersonal();
        }

        /// <summary>
        /// 定期券発売記録の編集
        /// </summary>
        /// <param name="newno">新カードＩｄｉ</param>
        /// <param name="oldno">旧カードＩｄｉ</param>
        /// <param name="number">券番号</param>
        /// <param name="iserr">発券前異常フラグ</param>
        /// <returns>券番号有無</returns>
        private bool EditofPass(ref string newno, ref string oldno, ref long number, bool iserr = false)
        {
            bool ret = false;
            // 支払方法
            JPDataEditForPayment();
            // 券番号、購入金額
            ret = JPDataEditForPassLine3(ref number, iserr);
            // 種別、定期券金額
            JPDataEditForPassLine4();
            // 媒体、チャージ金額
            JPDataEditForPassLine5();
            // 券種、デポジット金額
            JPDataEditForPassLine6();
            // 大小、投入金額
            JPDataEditForPassLine7();
            // 割引、おつり
            JPDataEditForPassLine8();
            // IDi
            JPDataEditForIDi(ref newno, ref oldno);
            // 個人データ
            JPDataEditForPersonal(true);
            // 定期券期間
            JPDataEditForPassTerm();
            // 定期券範囲
            JPDataEditForPassRange();

            return ret;
        }

        /// <summary>
        /// ジャーナル出力機能：廃札記録の作成
        /// </summary>
        private void JPPrintCreateDataForScrapRecord()
        {
            // 取引データ取得処理
            var time = session.StartTime.Value.Value;
            long number = 0;
            bool noflag = false;
            string oldno = string.Empty;
            string newno = string.Empty;

            // タイトル
            PrintData.Add(string.Format("廃札　　　　取引日時　{0:D4}年{1:D2}月{2:D2}日{3:D2}時{4:D2}分{5:D2}秒", time.Year, time.Month, time.Day, time.Hour, time.Minute, time.Second));
            // 支払方法
            JPDataEditForPayment();

            // 券番号、購入金額
            noflag = JPDataEditForPassLine3(ref number);
            // 種別、定期券金額
            JPDataEditForPassLine4();
            // 媒体、チャージ金額
            JPDataEditForPassLine5();
            // 券種、デポジット金額
            JPDataEditForPassLine6();
            // 大小、投入金額
            JPDataEditForPassLine7();
            // 割引、おつり
            JPDataEditForPassLine8();
            // IDi
            JPDataEditForIDi(ref newno, ref oldno);
            // 個人データ
            JPDataEditForPersonal(true);
            // 定期券期間
            JPDataEditForPassTerm();
            // 定期券範囲
            JPDataEditForPassRange();

            // ヘッダ部編集
            var freeFormatInput = new JpFreeFormatInput();
            freeFormatInput.PreFeed = 0;
            freeFormatInput.CutKind = JpFreeFormatInput.PaperCutKindType.NON_CUT;
            freeFormatInput.RawMode = false;
            freeFormatInput.PostFeed = 0;
            freeFormatInput.PrintInfo = PrintData.ToArray();
            var JpMediaInput = new MediaDataInput();
            JpMediaInput.EditKind = MediaDataInput.MediaEditKind.EditJpFreeFormat;
            JpMediaInput.Info = freeFormatInput.GetBytes();
            var scrapJournalData = new JournalHistory.IssueJournalData();
            scrapJournalData.PrintData = JpMediaInput.GetBytes();
            scrapJournalData.HeadData.PrintDateTime = time;
            scrapJournalData.HeadData.TicketNoSet = noflag;
            scrapJournalData.HeadData.TicketNo = (uint)number;
            if (string.IsNullOrEmpty(oldno))
            {
                // 旧カードのIDiがブランク
                scrapJournalData.HeadData.OldIDiSet = false;
            }
            else 
            {
                scrapJournalData.HeadData.OldIDiSet = true;
                scrapJournalData.HeadData.OldIDi = Strings.StrConv(oldno, VbStrConv.Narrow);
            }
            if (string.IsNullOrEmpty(newno))
            {
                // 新カードのIDiがブランク
                scrapJournalData.HeadData.IDiSet = false;
            }
            else
            {
                scrapJournalData.HeadData.IDiSet = true;
                scrapJournalData.HeadData.IDi = Strings.StrConv(newno, VbStrConv.Narrow);
            }

            DataStore.GetInstance().JournalHistory.Write((object)scrapJournalData);
        }

        /// <summary>
        /// ジャーナル出力機能：交換記録データの作成
        /// </summary>
        private void JPPrintCreateDataForExchangeRecord()
        {
            // 取引データ取得処理
            var time = session.StartTime.Value.Value;
            string newno = string.Empty;
            string oldno = string.Empty;
            bool numflag = false;
            long number = 0;

            // タイトル
            PrintData.Add(string.Format("交換　　　　取引日時　{0:D4}年{1:D2}月{2:D2}日{3:D2}時{4:D2}分{5:D2}秒", time.Year, time.Month, time.Day, time.Hour, time.Minute, time.Second));

            if (session.IcCardChange.Value)
            {
                // チャージの世代交換
                numflag = JPDataEditForChangeKind(CHANGEKIND.Change, ref number);
                // IDi
                JPDataEditForIDi(ref newno, ref oldno);
                // 個人データ
                JPDataEditForPersonal();
            }
            else
            {
                switch (session.Operation.Value)
                {
                    case Session.OperationType.IcNameChange:
                        // Ｍｙ　Ｓｕｉｃａへの交換
                        numflag = JPDataEditForChangeKind(CHANGEKIND.NameChange, ref number);
                        // IDi
                        JPDataEditForIDi(ref newno, ref oldno);
                        // 個人データ
                        JPDataEditForPersonal();
                        break;
                    case Session.OperationType.ICMoneyChange:
                        // 物販交換
                        numflag = JPDataEditForChangeKind(CHANGEKIND.MoneyChange, ref number);
                        // IDi
                        JPDataEditForIDi(ref newno, ref oldno);
                        // 個人データ
                        JPDataEditForPersonal();
                        break;
                    case Session.OperationType.Pass:
                        // 発行替え
                        numflag = JPDataEditForChangeKind(CHANGEKIND.Exchange, ref number);
                        // IDi
                        JPDataEditForIDi(ref newno, ref oldno);
                        // 個人データ
                        JPDataEditForPersonal(true);
                        // 定期券期間
                        JPDataEditForPassTerm();
                        // 定期券範囲
                        JPDataEditForPassRange();
                        break;
                    case Session.OperationType.IcAttributeChange:
                        // 属性変更
                        numflag = JPDataEditForChangeKind(CHANGEKIND.ICAttributeChange, ref number);
                        // IDi
                        JPDataEditForIDi(ref newno, ref oldno);
                        // 個人データ
                        JPDataEditForPersonal();
                        break;
                }
            }
            // ヘッダ部編集
            var freeFormatInput = new JpFreeFormatInput();
            freeFormatInput.PreFeed = 0;
            freeFormatInput.CutKind = JpFreeFormatInput.PaperCutKindType.NON_CUT;
            freeFormatInput.RawMode = false;
            freeFormatInput.PostFeed = 0;
            freeFormatInput.PrintInfo = PrintData.ToArray();
            var JpMediaInput = new MediaDataInput();
            JpMediaInput.EditKind = MediaDataInput.MediaEditKind.EditJpFreeFormat;
            JpMediaInput.Info = freeFormatInput.GetBytes();
            var exchangeJournalData = new JournalHistory.IssueJournalData();
            exchangeJournalData.PrintData = JpMediaInput.GetBytes();
            exchangeJournalData.HeadData.PrintDateTime = time;
            exchangeJournalData.HeadData.TicketNoSet = numflag;
            if (numflag)
            {
                exchangeJournalData.HeadData.TicketNo = (uint)number;
            }
            if (string.IsNullOrEmpty(oldno))
            {
                // 旧カードのIDiがブランク
                exchangeJournalData.HeadData.OldIDiSet = false;
            }
            else
            {
                exchangeJournalData.HeadData.OldIDiSet = true;
                exchangeJournalData.HeadData.OldIDi = Strings.StrConv(oldno, VbStrConv.Narrow);
            }
            if (string.IsNullOrEmpty(newno))
            {
                // 新カードのIDiがブランク
                exchangeJournalData.HeadData.IDiSet = false;
            }
            else
            {
                exchangeJournalData.HeadData.IDiSet = true;
                exchangeJournalData.HeadData.IDi = Strings.StrConv(newno, VbStrConv.Narrow);
            }

            DataStore.GetInstance().JournalHistory.Write((object)exchangeJournalData);
        }

        /// <summary>
        /// ジャーナル出力機能：再発記録の作成
        /// </summary>
        private void JPPrintCreateDataForReissueRecord()
        {
            // 取引データ取得処理
            var time = session.StartTime.Value.Value;
            string oldno = string.Empty;
            string newno = string.Empty;
            bool numflag = false;
            long number = 0;

            // タイトル
            PrintData.Add(string.Format("再発　　　　取引日時　{0:D4}年{1:D2}月{2:D2}日{3:D2}時{4:D2}分{5:D2}秒", time.Year, time.Month, time.Day, time.Hour, time.Minute, time.Second));

            if (session.Operation.Value == Session.OperationType.Pass)
            {
                if ((session.PassInfo.Value.ChoiceProcess == ChoiceProcessType.New) ||
                    (session.PassInfo.Value.ChoiceProcess == ChoiceProcessType.Continue) ||
                    (session.PassInfo.Value.ChoiceProcess == ChoiceProcessType.TakeOver))
                {
                    // 定期券新規、継続、引継継続発行
                    numflag = EditofPass(ref newno, ref oldno, ref number);
                }
                else if (session.PassInfo.Value.ChoiceProcess == ChoiceProcessType.Exchange)
                {
                    // 発行替え
                    numflag = JPDataEditForChangeKind(CHANGEKIND.Exchange, ref number);
                    // IDi
                    JPDataEditForIDi(ref newno, ref oldno);
                    // 個人データ
                    JPDataEditForPersonal(true);
                    // 定期券期間
                    JPDataEditForPassTerm();
                    // 定期券範囲
                    JPDataEditForPassRange();
                }
            }
            else if ((session.Operation.Value == Session.OperationType.SignIc) ||
                     (session.Operation.Value == Session.OperationType.UnsignIc))
            {
                // ＩＣカード発行
                EditofCardIssue(ref newno);
            }
            else if ((session.Operation.Value == Session.OperationType.Charge) &&
                      session.IcCardChange.Value)
            {
                // チャージの世代交換
                numflag = JPDataEditForChangeKind(CHANGEKIND.Change, ref number);
                // IDi
                JPDataEditForIDi(ref newno, ref oldno);
                // 個人データ
                JPDataEditForPersonal();
            }
            else if (session.Operation.Value == Session.OperationType.IcNameChange)
            {
                // Ｍｙ　Ｓｕｉｃａへの交換
                numflag = JPDataEditForChangeKind(CHANGEKIND.NameChange, ref number);
                // IDi
                JPDataEditForIDi(ref newno, ref oldno);
                // 個人データ
                JPDataEditForPersonal();
            }
            else if (session.Operation.Value == Session.OperationType.ICMoneyChange)
            {
                // 物販交換
                numflag = JPDataEditForChangeKind(CHANGEKIND.MoneyChange, ref number);
                // IDi
                JPDataEditForIDi(ref newno, ref oldno);
                // 個人データ
                JPDataEditForPersonal();
            }
            else if (session.Operation.Value == Session.OperationType.IcAttributeChange)
            {
                // 属性変更
                numflag = JPDataEditForChangeKind(CHANGEKIND.ICAttributeChange, ref number);
                // IDi
                JPDataEditForIDi(ref newno, ref oldno);
                // 個人データ
                JPDataEditForPersonal();
            }
            // ヘッダ部編集
            var freeFormatInput = new JpFreeFormatInput();
            freeFormatInput.PreFeed = 0;
            freeFormatInput.CutKind = JpFreeFormatInput.PaperCutKindType.NON_CUT;
            freeFormatInput.RawMode = false;
            freeFormatInput.PostFeed = 0;
            freeFormatInput.PrintInfo = PrintData.ToArray();
            var JpMediaInput = new MediaDataInput();
            JpMediaInput.EditKind = MediaDataInput.MediaEditKind.EditJpFreeFormat;
            JpMediaInput.Info = freeFormatInput.GetBytes();
            var reissueJournalData = new JournalHistory.IssueJournalData();
            reissueJournalData.PrintData = JpMediaInput.GetBytes();
            reissueJournalData.HeadData.PrintDateTime = time;
            reissueJournalData.HeadData.TicketNoSet = numflag;
            if (numflag)
            {
                reissueJournalData.HeadData.TicketNo = (uint)number;
            }
            if (string.IsNullOrEmpty(oldno))
            {
                // 旧カードのIDiがブランク
                reissueJournalData.HeadData.OldIDiSet = false;
            }
            else
            {
                reissueJournalData.HeadData.OldIDiSet = true;
                reissueJournalData.HeadData.OldIDi = Strings.StrConv(oldno, VbStrConv.Narrow);
            }
            if (string.IsNullOrEmpty(newno))
            {
                // 新カードのIDiがブランク
                reissueJournalData.HeadData.IDiSet = false;
            }
            else
            {
                reissueJournalData.HeadData.IDiSet = true;
                reissueJournalData.HeadData.IDi = Strings.StrConv(newno, VbStrConv.Narrow);
            }

            DataStore.GetInstance().JournalHistory.Write((object)reissueJournalData);
        }

        /// <summary>
        /// ジャーナル出力機能：締切記録の作成
        /// </summary>
        private void JPPrintCreateDataForClosingRecord()
        {
            // 集計情報の参照
            TotalizeTotalData = new TotalizeDataSearch(TotalizeDataCount.GetInstance().GetData(DataCountBase.Generation.Cur, TotalizeDataCount.TotalizeKind.Total, DataCountLabels.CategoryPrintData));

            // タイトル、管理日計日
            // 設置駅コード、コーナ、号機
            JPDataEditForClosingTitle();

            // きっぷ枚数／金額
            JPDataEditForTiecket();

            // 定期券
            JPDataEditForPass();

            // チャージ
            JPDataEditForCharge();

            // デポジット徴収
            JPDataEditForDiposit();

            // デポジット返却
            JPDataEditForDipositRest();

            // Ｓｕｉｃａ
            JPDataEditForIC();

            // 回収枚数
            JPDataEditForCollect();

            // 試刷枚数
            JPDataEditForTest();

            // 金庫枚数
            JPDataEditForCoffer();

            // 単発枚数
            JPDataEditForSingle();

            // 取忘枚数
            JPDataEditForForget();

            // ＳＦ返金
            JPDataEditForSfback();

            // ポイントチャージ
            JPDataEditForPoint();

            // 空行
            PrintData.Add(string.Empty);

            // ヘッダ部編集
            var freeFormatInput = new JpFreeFormatInput();
            freeFormatInput.PreFeed = 0;
            freeFormatInput.CutKind = JpFreeFormatInput.PaperCutKindType.NON_CUT;
            freeFormatInput.RawMode = false;
            freeFormatInput.PostFeed = 0;
            freeFormatInput.PrintInfo = PrintData.ToArray();
            var JpMediaInput = new MediaDataInput();
            JpMediaInput.EditKind = MediaDataInput.MediaEditKind.EditJpFreeFormat;
            JpMediaInput.Info = freeFormatInput.GetBytes();
            var closeJournalData = new JournalHistory.OtherJournalData();
            closeJournalData.PrintData = JpMediaInput.GetBytes();
            closeJournalData.HeadData.PrintDateTime = System.DateTime.Now;
            DataStore.GetInstance().JournalHistory.Write((object)closeJournalData);
        }

        /// <summary>
        /// ジャーナル出力機能：印刷開始前発券異常記録の作成
        /// </summary>
        private void JPPrintCreateDataForErrorRecord()
        {
            var time = System.DateTime.Now;
            string oldno = string.Empty;
            string newno = string.Empty;
            bool numflag = false;
            long number = 0;

            // タイトル
            PrintData.Add("＜＜＜売上未計上発行券情報＞＞＞");
            // 異常内容
            Errors errors = DataStore.GetInstance().Errors;
            // 異常情報を表示
            string errorWord = string.Empty;
            foreach (var value in errors.ErrorList.Values)
            {
                errorWord = ErrorCodeConverter.Convert(value.EC).Error;
                PrintData.Add("異常内容　" + errorWord);
            }

            if (session.Operation.Value == Session.OperationType.Pass)
            {
                if ((session.PassInfo.Value.ChoiceProcess == ChoiceProcessType.New) ||
                    (session.PassInfo.Value.ChoiceProcess == ChoiceProcessType.Continue) ||
                    (session.PassInfo.Value.ChoiceProcess == ChoiceProcessType.TakeOver))
                {
                    // 定期券新規、継続、引継継続発行
                    EditofPass(ref newno, ref oldno, ref number, true);
                }
                else if (session.PassInfo.Value.ChoiceProcess == ChoiceProcessType.Exchange)
                {
                    // 発行替え
                    numflag = JPDataEditForChangeKind(CHANGEKIND.Exchange, ref number, true);
                    // IDi
                    JPDataEditForIDi(ref newno, ref oldno);
                    // 個人データ
                    JPDataEditForPersonal(true);
                    // 定期券期間
                    JPDataEditForPassTerm();
                    // 定期券範囲
                    JPDataEditForPassRange();
                }
            }
            else if ((session.Operation.Value == Session.OperationType.SignIc) ||
                     (session.Operation.Value == Session.OperationType.UnsignIc))
            {
                // ＩＣカード発行
                EditofCardIssue(ref newno);
            }
            else if ((session.Operation.Value == Session.OperationType.Charge) &&
                      session.IcCardChange.Value)
            {
                // チャージの世代交換
                numflag = JPDataEditForChangeKind(CHANGEKIND.Change, ref number, true);
                // IDi
                JPDataEditForIDi(ref newno, ref oldno);
                // 個人データ
                JPDataEditForPersonal();
            }
            else if (session.Operation.Value == Session.OperationType.IcNameChange)
            {
                // Ｍｙ　Ｓｕｉｃａへの交換
                numflag = JPDataEditForChangeKind(CHANGEKIND.NameChange, ref number, true);
                // IDi
                JPDataEditForIDi(ref newno, ref oldno);
                // 個人データ
                JPDataEditForPersonal();
            }
            else if (session.Operation.Value == Session.OperationType.ICMoneyChange)
            {
                // 物販交換
                numflag = JPDataEditForChangeKind(CHANGEKIND.MoneyChange, ref number, true);
                // IDi
                JPDataEditForIDi(ref newno, ref oldno);
                // 個人データ
                JPDataEditForPersonal();
            }
            else if (session.Operation.Value == Session.OperationType.IcAttributeChange)
            {
                // 属性変更
                numflag = JPDataEditForChangeKind(CHANGEKIND.ICAttributeChange, ref number, true);
                // IDi
                JPDataEditForIDi(ref newno, ref oldno);
                // 個人データ
                JPDataEditForPersonal();
            }
            // ヘッダ部編集
            var freeFormatInput = new JpFreeFormatInput();
            freeFormatInput.PreFeed = 0;
            freeFormatInput.CutKind = JpFreeFormatInput.PaperCutKindType.NON_CUT;
            freeFormatInput.RawMode = false;
            freeFormatInput.PostFeed = 0;
            freeFormatInput.PrintInfo = PrintData.ToArray();
            var JpMediaInput = new MediaDataInput();
            JpMediaInput.EditKind = MediaDataInput.MediaEditKind.EditJpFreeFormat;
            JpMediaInput.Info = freeFormatInput.GetBytes();
            var abnormalJournalData = new JournalHistory.IssueJournalData();
            abnormalJournalData.PrintData = JpMediaInput.GetBytes();
            abnormalJournalData.HeadData.PrintDateTime = time;
            abnormalJournalData.HeadData.TicketNoSet = false;
            if (string.IsNullOrEmpty(oldno))
            {
                // 旧カードのIDiがブランク
                abnormalJournalData.HeadData.OldIDiSet = false;
            }
            else
            {
                abnormalJournalData.HeadData.OldIDiSet = true;
                abnormalJournalData.HeadData.OldIDi = oldno;
            }
            if (string.IsNullOrEmpty(newno))
            {
                // 新カードのIDiがブランク
                abnormalJournalData.HeadData.IDiSet = false;
            }
            else
            {
                abnormalJournalData.HeadData.IDiSet = true;
                abnormalJournalData.HeadData.IDi = newno;
            }

            DataStore.GetInstance().JournalHistory.Write((object)abnormalJournalData);
        }

        /// <summary>
        /// ジャーナル出力機能：与信受信データの作成
        /// </summary>
        /// <param name="request">売上要求メッセージ</param>
        /// <param name="response">売上結果メッセージ</param>
        /// <param name="isresult">要求結果フラグ(True:要求結果電文、False:要求電文)</param>
        /// <param name="isOK">要求結果OK/NG(True:OK、False:NG)</param>
        private void JPPrintCreateDataForSale(object request, object response, bool isresult, bool isOK)
        {
            bool result = false;
            long num = 0;
            if (!isresult)
            {
                // 売上要求電文送信時
                JPDataEditForSaleRequest(request);
            }
            else
            {
                // 売上結果データの作成
                JPDataEditForSaleResult(request, response, isOK);
            }
            /////////////////////////////////////////////////
            // ヘッダ部編集
            var freeFormatInput = new JpFreeFormatInput();
            freeFormatInput.PreFeed = 0;
            freeFormatInput.CutKind = JpFreeFormatInput.PaperCutKindType.NON_CUT;
            freeFormatInput.RawMode = false;
            freeFormatInput.PostFeed = 0;
            freeFormatInput.PrintInfo = PrintData.ToArray();
            var JpMediaInput = new MediaDataInput();
            JpMediaInput.EditKind = MediaDataInput.MediaEditKind.EditJpFreeFormat;
            JpMediaInput.Info = freeFormatInput.GetBytes();
            var viewJournalData = new JournalHistory.ViewJournalData();
            viewJournalData.PrintData = JpMediaInput.GetBytes();
            viewJournalData.HeadData.PrintDateTime = System.DateTime.Now;
            result = NumberJudge(ref num);
            if (isresult && result)
            {
                viewJournalData.HeadData.TicketNoSet = true;
            }
            else
            {
                viewJournalData.HeadData.TicketNoSet = false;
            }
            if (viewJournalData.HeadData.TicketNoSet)
            {
                viewJournalData.HeadData.TicketNo = (uint)num;
            }
            DataStore.GetInstance().JournalHistory.Write((object)viewJournalData);
        }

        /// <summary>
        /// ジャーナル出力機能：内部控除処理データの作成
        /// </summary>
        /// <param name="request">内部控除要求メッセージ</param>
        /// <param name="response">内部控除結果メッセージ</param>
        /// <param name="isresult">要求結果フラグ(True:要求結果電文、False:要求電文)</param>
        /// <param name="isOK">要求結果OK/NG(True:OK、False:NG)</param>
        private void JPPrintCreateDataForDeduct(object request, object response, bool isresult, bool isOK)
        {
            bool result = false;
            long num = 0;
            if (!isresult)
            {
                // 内部控除要求電文送信時
                JPDataEditForDeductRequest(request);
            }
            else
            {
                // 内部控除結果データの作成
                JPDataEditForDeductResult(request, response, isOK);
            }

            /////////////////////////////////////////////////
            // ヘッダ部編集
            var freeFormatInput = new JpFreeFormatInput();
            freeFormatInput.PreFeed = 0;
            freeFormatInput.CutKind = JpFreeFormatInput.PaperCutKindType.NON_CUT;
            freeFormatInput.RawMode = false;
            freeFormatInput.PostFeed = 0;
            freeFormatInput.PrintInfo = PrintData.ToArray();
            var JpMediaInput = new MediaDataInput();
            JpMediaInput.EditKind = MediaDataInput.MediaEditKind.EditJpFreeFormat;
            JpMediaInput.Info = freeFormatInput.GetBytes();
            var viewJournalData = new JournalHistory.ViewJournalData();
            viewJournalData.PrintData = JpMediaInput.GetBytes();
            viewJournalData.HeadData.PrintDateTime = System.DateTime.Now;
            result = NumberJudge(ref num);
            if (isresult && result)
            {
                viewJournalData.HeadData.TicketNoSet = true;
            }
            else
            {
                viewJournalData.HeadData.TicketNoSet = false;
            }
            if (viewJournalData.HeadData.TicketNoSet)
            {
                viewJournalData.HeadData.TicketNo = (uint)num;
            }
            DataStore.GetInstance().JournalHistory.Write((object)viewJournalData);
        }

        /// <summary>
        /// ジャーナル出力機能：売上戻し処理結果データの作成
        /// </summary>
        /// <param name="request">売上戻し要求メッセージ</param>
        /// <param name="response">売上戻し結果メッセージ</param>
        /// <param name="isresult">要求結果フラグ(True:要求結果電文、False:要求電文)</param>
        /// <param name="isOK">要求結果OK/NG(True:OK、False:NG)</param>
        private void JPPrintCreateDataForRefundResult(object request, object response, bool isresult, bool isOK)
        {
            bool result = false;
            long num = 0;

            if (!isresult)
            {
                // 売上戻し要求電文送信時
                JPDataEditForRefundRequest(request);
            }
            else
            {
                // 売上戻し結果データの作成
                JPDataEditFoRefundResult(request, response, isOK);
            }
            /////////////////////////////////////////////////
            // ヘッダ部編集
            var freeFormatInput = new JpFreeFormatInput();
            freeFormatInput.PreFeed = 0;
            freeFormatInput.CutKind = JpFreeFormatInput.PaperCutKindType.NON_CUT;
            freeFormatInput.RawMode = false;
            freeFormatInput.PostFeed = 0;
            freeFormatInput.PrintInfo = PrintData.ToArray();
            var JpMediaInput = new MediaDataInput();
            JpMediaInput.EditKind = MediaDataInput.MediaEditKind.EditJpFreeFormat;
            JpMediaInput.Info = freeFormatInput.GetBytes();
            var viewJournalData = new JournalHistory.ViewJournalData();
            viewJournalData.PrintData = JpMediaInput.GetBytes();
            viewJournalData.HeadData.PrintDateTime = System.DateTime.Now;
            result = NumberJudge(ref num);
            if (isresult && result)
            {
                viewJournalData.HeadData.TicketNoSet = true;
            }
            else
            {
                viewJournalData.HeadData.TicketNoSet = false;
            }
            if (viewJournalData.HeadData.TicketNoSet)
            {
                viewJournalData.HeadData.TicketNo = (uint)num;
            }
            DataStore.GetInstance().JournalHistory.Write((object)viewJournalData);
        }

        /// <summary>
        /// ジャーナル出力機能：精算処理データの作成
        /// </summary>
        /// <param name="request">カウンタ要求メッセージ</param>
        /// <param name="response">カウンタ結果メッセージ</param>
        /// <param name="isresult">要求結果フラグ(True:要求結果電文、False:要求電文)</param>
        /// <param name="isOK">要求結果OK/NG(True:OK、False:NG)</param>
        private void JPPrintCreateDataForAdjust(object request, object response, bool isresult, bool isOK)
        {
            if (!isresult)
            {
                // カウンタ要求電文送信時
                JPDataEditForCountRequest(request);
            }
            else 
            {
                // カウンタ通知電文結果データの作成
                JPDataEditForCountResult(request, response, isOK);
                JPPrintCreateDataForViewScrap();
            }

            // ヘッダ部編集
            var freeFormatInput = new JpFreeFormatInput();
            freeFormatInput.PreFeed = 0;
            freeFormatInput.CutKind = JpFreeFormatInput.PaperCutKindType.NON_CUT;
            freeFormatInput.RawMode = false;
            freeFormatInput.PostFeed = 0;
            freeFormatInput.PrintInfo = PrintData.ToArray();
            var JpMediaInput = new MediaDataInput();
            JpMediaInput.EditKind = MediaDataInput.MediaEditKind.EditJpFreeFormat;
            JpMediaInput.Info = freeFormatInput.GetBytes();
            var otherJournalData = new JournalHistory.OtherJournalData();
            otherJournalData.PrintData = JpMediaInput.GetBytes();
            otherJournalData.HeadData.PrintDateTime = System.DateTime.Now;
            otherJournalData.PrintData = JpMediaInput.GetBytes();
            DataStore.GetInstance().JournalHistory.Write((object)otherJournalData);
        }

        /// <summary>
        /// ジャーナル出力機能：開局処理データの作成
        /// </summary>
        /// <param name="request">開局要求メッセージ</param>
        /// <param name="response">開局結果メッセージ</param>
        /// <param name="isresult">要求結果フラグ(True:要求結果電文、False:要求電文)</param>
        /// <param name="isOK">要求結果OK/NG(True:OK、False:NG)</param>
        private void JPPrintCreateDataForOpen(object request, object response, bool isresult, bool isOK)
        {
            if (!isresult)
            {
                // 開局要求電文送信時
                JPDataEditForOpenRequest(request);
            }
            else
            {
                // 開局結果データの作成
                JPDataEditForOpenResult(request, response, isOK);
            }
            // ヘッダ部編集
            var freeFormatInput = new JpFreeFormatInput();
            freeFormatInput.PreFeed = 0;
            freeFormatInput.CutKind = JpFreeFormatInput.PaperCutKindType.NON_CUT;
            freeFormatInput.RawMode = false;
            freeFormatInput.PostFeed = 0;
            freeFormatInput.PrintInfo = PrintData.ToArray();
            var JpMediaInput = new MediaDataInput();
            JpMediaInput.EditKind = MediaDataInput.MediaEditKind.EditJpFreeFormat;
            JpMediaInput.Info = freeFormatInput.GetBytes();
            var otherJournalData = new JournalHistory.OtherJournalData();
            otherJournalData.PrintData = JpMediaInput.GetBytes();
            otherJournalData.HeadData.PrintDateTime = System.DateTime.Now;
            otherJournalData.PrintData = JpMediaInput.GetBytes();
            DataStore.GetInstance().JournalHistory.Write((object)otherJournalData);
        }

        /// <summary>
        /// ジャーナル出力機能：都度カウンタ異常印字データの作成
        /// </summary>
        /// <param name="isflag">精算処理印字フラグ</param>
        /// <param name="request">要求電文</param>
        /// <param name="response">要求結果電文</param>
        private void JPPrintCreateDataForErrorPrint(bool isflag, object request, object response)
        {
            if (isflag)
            {
                // 精算処理印字の後につづけて印字
                PrintData.Add("－－カウンタ不一致－－");
                JPDataEditForErrorAdjust(request, response);
            }
            else
            {
                // 精算処理以外の時、各処理印字の後につづけて印字
                PrintData.Add("－－都度カウンタ不一致－－");
                JPDataEditForErrorPrint(request, response);
            }
        }

        /// <summary>
        /// ジャーナル出力機能：ビュー・スイカ定期券廃札ＩＤｉデータの作成
        /// </summary>
        private void JPPrintCreateDataForViewScrap()
        {
            List<string> Idi = new List<string>();
            // タイトル、日付
            // 設置駅コード、コーナ、号機
            JPDataEditForViewScrapTitle();

            // 廃札ＩＤｉ
            Idi = JPDataEditForScrapIdi();
        }

        /// <summary>
        /// ジャーナル出力機能：処理開始データの作成
        /// </summary>
        private void JPPrintCreateDataForStart()
        {
            PrintData.Add("－－－－－－－－－－－－－－－－－－－－－－－－");
            // ヘッダ部編集
            var freeFormatInput = new JpFreeFormatInput();
            freeFormatInput.PreFeed = 0;
            freeFormatInput.CutKind = JpFreeFormatInput.PaperCutKindType.NON_CUT;
            freeFormatInput.RawMode = false;
            freeFormatInput.PostFeed = 0;
            freeFormatInput.PrintInfo = PrintData.ToArray();
            var JpMediaInput = new MediaDataInput();
            JpMediaInput.EditKind = MediaDataInput.MediaEditKind.EditJpFreeFormat;
            JpMediaInput.Info = freeFormatInput.GetBytes();
            var otherJournalData = new JournalHistory.OtherJournalData();
            otherJournalData.PrintData = JpMediaInput.GetBytes();
            otherJournalData.HeadData.PrintDateTime = System.DateTime.Now;
            otherJournalData.PrintData = JpMediaInput.GetBytes();
            DataStore.GetInstance().JournalHistory.Write((object)otherJournalData);
        }

        /// <summary>
        /// 開始記録データの編集処理
        /// </summary>
        private void JPDataEditForStartTitle()
        {
            // タイトル
            PrintData.Add("＊＊＊＊＊＊＊＊＊　開　　始　＊＊＊＊＊＊＊＊＊");

            MyDate myDate = new MyDate();
            long value = 0;
            // 管理日計日
            Dictionary<string, long> managedata = TotalizeDataCount.GetInstance().GetData(DataCountBase.Generation.Cur, TotalizeDataCount.TotalizeKind.Day, DataCountLabels.CategoryManagementDate);
            if (managedata.TryGetValue(DataCountLabels.ManagementDate, out value))
            {
                myDate = MyDate.FromYYYYMMDD(value.ToString());
            }
            PrintData.Add(string.Format("＊　管理日計日：{0:D4}年{1:D2}月{2:D2}日　　　　　　　　＊", myDate.Year, myDate.Month, myDate.Day));

            // 収入管理日
            Dictionary<string, long> incomedata = TotalizeDataCount.GetInstance().GetData(DataCountBase.Generation.Cur, TotalizeDataCount.TotalizeKind.Day, DataCountLabels.CategoryInComeDate);
            if (incomedata.TryGetValue(DataCountLabels.InComeDate, out value))
            {
                myDate = MyDate.FromYYYYMMDD(value.ToString());
            }
            PrintData.Add(string.Format("＊　収入管理日：{0:D4}年{1:D2}月{2:D2}日　　　　　　　　＊", myDate.Year, myDate.Month, myDate.Day));

            string stationName = string.Empty;
            var operationalDateTime = new OperationalDateTime(System.DateTime.Now);
            stationName = UtilityInterface.GetStationInfo(settings.Line, settings.Station, UtilityInterface.StationNameUserFourJpn, operationalDateTime.GetThreeHourDate());

            // 設置駅名、コーナ、号機
            PrintData.Add(string.Format("＊　設置駅：{0}　コーナ： {1}　号機：{2:D2}　　＊", stationName.PadRight(4, '　'), GetPrintCorner(), Convert.ToInt32(settings.PrintNo.Value)));
            PrintData.Add("＊＊＊＊＊＊＊＊＊＊＊＊＊＊＊＊＊＊＊＊＊＊＊＊");
            PrintData.Add(string.Empty);
        }

        /// <summary>
        /// タイトル、管理日計日、設置駅コードおよびコーナ、号機（多機能券売機用）
        /// </summary>
        private void JPDataEditForClosingTitle()
        {
            // タイトル
            PrintData.Add("＊＊＊＊＊＊＊＊＊　締　　切　＊＊＊＊＊＊＊＊＊");

            MyDate myDate = new MyDate();
            long value = 0;
            // 管理日計日
            Dictionary<string, long> managedata = TotalizeDataCount.GetInstance().GetData(DataCountBase.Generation.Cur, TotalizeDataCount.TotalizeKind.Day, DataCountLabels.CategoryManagementDate);
            if (managedata.TryGetValue(DataCountLabels.ManagementDate, out value))
            {
                myDate = MyDate.FromYYYYMMDD(value.ToString());
            }
            PrintData.Add(string.Format("　　　　　　　　　　　　{0:D4}年  {1:D2}月  {2:D2}日", myDate.Year, myDate.Month, myDate.Day));

            string machineNo = settings.PrintNo.Value;
            // 設置駅コード、コーナ、号機
            PrintData.Add(string.Format("　設置駅コード　{0:D6}　　　 {1}コーナ　{2:D2}号機", settings.HostCode.Value, GetPrintCorner(), Convert.ToInt32(machineNo)));
            // 空行
            PrintData.Add(string.Empty);
            PrintData.Add("＊＊＊＊＊＊＊＊＊＊＊＊＊＊＊＊＊＊＊＊＊＊＊＊");
            // 空行
            PrintData.Add(string.Empty);
        }

        /// <summary>
        /// 印字用コーナ変換処理
        /// </summary>
        /// <returns>印字用コーナ</returns>
        private string GetPrintCorner()
        {
            string corner = settings.PrintCorner.Value;
            int cornerNo = int.Parse(corner);
            if (cornerNo >= 0 && cornerNo <= 9)
            {
                // 00～09の場合、「0」抜く一桁印字
                corner = cornerNo.ToString();
            }
            else if (cornerNo >= 10 && cornerNo <= 35)
            {
                // 10～35の場合、「A」～「Z」を転換して一桁印字
                char buf = (char)((int)'A' + cornerNo - 10);
                corner = buf.ToString();
            }
            else
            {
                // 上記以外の場合、ブランク印字
                corner = "　";
            }

            return corner;
        }

        /// <summary>
        /// きっぷ枚数／金額
        /// </summary>
        private void JPDataEditForTiecket()
        {
            // タイトル
            PrintData.Add("きっぷ");
            // 売上枚数
            long num = TotalizeTotalData.GetData(DataCountLabels.TotalSaleNum, 6);
            PrintData.Add(string.Format("　売上枚数　　　　　　　　　　{0,6}枚", num));
            // 売上金額
            long amount = TotalizeTotalData.GetData(DataCountLabels.TotalSaleAmount, 8);
            PrintData.Add(string.Format("　売上金額　　　　　　　　　{0,8}円", amount));
            // 現金売上金額
            amount = TotalizeTotalData.GetData(DataCountLabels.TotalCashSaleAmount, 8);
            PrintData.Add(string.Format("　現金売上金額　　　　　　　{0,8}円", amount));
            // カード売上金額
            amount = TotalizeTotalData.GetData(DataCountLabels.TotalMagCardSaleAmount, 8);
            PrintData.Add(string.Format("　カード売上金額　　　　　　{0,8}円", amount));
            // ＩＣカード売上金額
  　        amount = TotalizeTotalData.GetData(DataCountLabels.TotalIcCardSaleAmount, 8);
            PrintData.Add(string.Format("　ＩＣカード売上金額　　　　{0,8}円", amount));
            // 自社完結売上枚数
            num = TotalizeTotalData.GetData(DataCountLabels.TotalOwnSaleNum, 6);
            PrintData.Add(string.Format("　自社完結売上枚数　　　　　　{0,6}枚", num));
            // 自社完結売上金額
            amount = TotalizeTotalData.GetData(DataCountLabels.TotalOwnSaleAmount, 8);
            PrintData.Add(string.Format("　自社完結売上金額　　　　　{0,8}円", amount));
            // 他社関連売上枚数
            num = TotalizeTotalData.GetData(DataCountLabels.TotalOthSaleNum, 6);
            PrintData.Add(string.Format("　他社関連売上枚数　　　　　　{0,6}枚", num));
            // 他社関連売上金額
            amount = TotalizeTotalData.GetData(DataCountLabels.TotalOthSaleAmount, 8);
            PrintData.Add(string.Format("　他社関連売上金額　　　　　{0,8}円", amount));
            // 空行
            PrintData.Add(string.Empty);

            // 異常券枚数
            num = TotalizeTotalData.GetData(DataCountLabels.PurchaseAbnormalNum, 6);
            PrintData.Add(string.Format("　異常券枚数　　　　　　　　　{0,6}枚", num));
            // 廃札券枚数
            num = TotalizeTotalData.GetData(DataCountLabels.PurchaseScrapNum, 6);
            PrintData.Add(string.Format("　廃札券枚数　　　　　　　　　{0,6}枚", num));
            // 誤購入払戻枚数
            num = TotalizeTotalData.GetData(DataCountLabels.TicketRefundNum, 6);
            PrintData.Add(string.Format("　誤購入払戻枚数　　　　　　　{0,6}枚", num));
            // 誤購入払戻金額
            amount = TotalizeTotalData.GetData(DataCountLabels.TicketRefundAmount, 8);
            PrintData.Add(string.Format("　誤購入払戻金額　　　　　　{0,8}円", amount));
            // 空行
            PrintData.Add(string.Empty);
        }

        /// <summary>
        /// 定期券
        /// </summary>
        private void JPDataEditForPass()
        {
            // タイトル
            PrintData.Add("定期券　＊＊＊＊＊＊＊＊＊＊＊＊＊＊＊＊＊＊＊＊");
            // 空行
            PrintData.Add(string.Empty);

            // 現金タイトル
            PrintData.Add("　現金売上");
            // 自社完結新規磁気定期券現金売上枚数
            long num = TotalizeTotalData.GetData(DataCountLabels.SeasonNewTotalOwnCashSaleNum, 6);
            // 自社完結新規磁気定期券現金売上金額
            long amount = TotalizeTotalData.GetData(DataCountLabels.SeasonNewTotalOwnCashSaleAmount, 9);
            PrintData.Add("　　新規（磁気定期券）");
            PrintData.Add(string.Format("　　　自社完結　　{0,6}枚　　　　 {1,9}円", num, amount));
            // 他社関連新規磁気定期券現金売上枚数
            num = TotalizeTotalData.GetData(DataCountLabels.SeasonNewTotalOtherCashSaleNum, 6);
            // 他社関連新規磁気定期券現金売上金額
            amount = TotalizeTotalData.GetData(DataCountLabels.SeasonNewTotalOtherCashSaleAmount, 9);
            PrintData.Add(string.Format("　　　他社関連　　{0,6}枚　　　　 {1,9}円", num, amount));
            // 空行
            PrintData.Add(string.Empty);

            // 自社完結継続磁気定期券現金売上枚数
            num = TotalizeTotalData.GetData(DataCountLabels.SeasonContinueTotalOwnCashSaleAmount, 6);
            // 自社完結継続磁気定期券現金売上金額
            amount = TotalizeTotalData.GetData(DataCountLabels.SeasonContinueTotalOwnCashSaleNum, 9);
            PrintData.Add("　　継続（磁気定期券）");
            PrintData.Add(string.Format("　　　自社完結　　{0,6}枚　　　　 {1,9}円", num, amount));
            // 他社関連継続磁気定期券現金売上枚数
            num = TotalizeTotalData.GetData(DataCountLabels.SeasonContinueTotalOtherCashSaleNum, 6);
            // 他社関連継続磁気定期券現金売上金額
            amount = TotalizeTotalData.GetData(DataCountLabels.SeasonContinueTotalOtherCashSaleAmount, 9);
            PrintData.Add(string.Format("　　　他社関連　　{0,6}枚　　　　 {1,9}円", num, amount));
            // 空行
            PrintData.Add(string.Empty);

            // 自社完結新規Ｓｕｉｃａ定期券現金売上枚数
            num = TotalizeTotalData.GetData(DataCountLabels.SeasonSuicaNewTotalOwnCashSaleNum, 6);
            // 自社完結新規Ｓｕｉｃａ定期券現金売上金額
            amount = TotalizeTotalData.GetData(DataCountLabels.SeasonSuicaNewTotalOwnCashSaleAmount, 9);
            PrintData.Add("　　新規（Ｓｕｉｃａ定期券）");
            PrintData.Add(string.Format("　　　自社完結　　{0,6}枚　　　　 {1,9}円", num, amount));
            // 他社関連新規Ｓｕｉｃａ定期券現金売上枚数
            num = TotalizeTotalData.GetData(DataCountLabels.SeasonSuicaNewTotalOtherCashSaleNum, 6);
            // 他社関連新規Ｓｕｉｃａ定期券現金売上金額
            amount = TotalizeTotalData.GetData(DataCountLabels.SeasonSuicaNewTotalOtherCashSaleAmount, 9);
            PrintData.Add(string.Format("　　　他社関連　　{0,6}枚　　　　 {1,9}円", num, amount));
            // 空行
            PrintData.Add(string.Empty);

            // 自社完結継続Ｓｕｉｃａ定期券現金売上枚数
            num = TotalizeTotalData.GetData(DataCountLabels.SeasonSuicaContinueTotalOwnCashSaleNum, 6);
            // 自社完結継続Ｓｕｉｃａ定期券現金売上金額
            amount = TotalizeTotalData.GetData(DataCountLabels.SeasonSuicaContinueTotalOwnCashSaleAmount, 9);
            PrintData.Add("　　継続（Ｓｕｉｃａ定期券）");
            PrintData.Add(string.Format("　　　自社完結　　{0,6}枚　　　　 {1,9}円", num, amount));
            // 他社関連継続Ｓｕｉｃａ定期券現金売上枚数
            num = TotalizeTotalData.GetData(DataCountLabels.SeasonSuicaContinueTotalOtherCashSaleNum, 6);
            // 他社関連継続Ｓｕｉｃａ定期券現金売上金額
            amount = TotalizeTotalData.GetData(DataCountLabels.SeasonSuicaContinueTotalOtherCashSaleAmount, 9);
            PrintData.Add(string.Format("　　　他社関連　　{0,6}枚　　　　 {1,9}円", num, amount));
            // 空行
            PrintData.Add(string.Empty);

            // クレジットタイトル
            PrintData.Add("　クレジット売上");
            // 自社完結新規磁気定期券クレジット売上枚数
            num = TotalizeTotalData.GetData(DataCountLabels.SeasonNewTotalOwnCreditSaleNum, 6);
            // 自社完結新規磁気定期券クレジット売上金額
            amount = TotalizeTotalData.GetData(DataCountLabels.SeasonNewTotalOwnCreditSaleAmount, 9);
            PrintData.Add("　　新規（磁気定期券）");
            PrintData.Add(string.Format("　　　自社完結　　{0,6}枚　　　　 {1,9}円", num, amount));
            // 他社関連新規磁気定期券クレジット売上枚数
            num = TotalizeTotalData.GetData(DataCountLabels.SeasonNewTotalOtherCreditSaleNum, 6);
            // 他社関連新規磁気定期券クレジット売上金額
            amount = TotalizeTotalData.GetData(DataCountLabels.SeasonNewTotalOtherCreditSaleAmount, 9);
            PrintData.Add(string.Format("　　　他社関連　　{0,6}枚　　　　 {1,9}円", num, amount));
            // 空行
            PrintData.Add(string.Empty);

            // 自社完結継続磁気定期券クレジット売上枚数
            num = TotalizeTotalData.GetData(DataCountLabels.SeasonContinueTotalOwnCreditSaleNum, 6);
            // 自社完結継続磁気定期券クレジット売上金額
            amount = TotalizeTotalData.GetData(DataCountLabels.SeasonContinueTotalOwnCreditSaleAmount, 9);
            PrintData.Add("　　継続（磁気定期券）");
            PrintData.Add(string.Format("　　　自社完結　　{0,6}枚　　　　 {1,9}円", num, amount));
            // 他社関連継続磁気定期券クレジット売上枚数
            num = TotalizeTotalData.GetData(DataCountLabels.SeasonContinueTotalOtherCreditSaleNum, 6);
            // 他社関連継続磁気定期券クレジット売上金額
            amount = TotalizeTotalData.GetData(DataCountLabels.SeasonContinueTotalOtherCreditSaleAmount, 9);
            PrintData.Add(string.Format("　　　他社関連　　{0,6}枚　　　　 {1,9}円", num, amount));
            // 空行
            PrintData.Add(string.Empty);

            // 自社完結新規Ｓｕｉｃａ定期券クレジット売上枚数
            num = TotalizeTotalData.GetData(DataCountLabels.SeasonSuicaNewTotalOwnCreditSaleNum, 6);
            // 自社完結新規Ｓｕｉｃａ定期券クレジット売上金額
            amount = TotalizeTotalData.GetData(DataCountLabels.SeasonSuicaNewTotalOwnCreditSaleAmount, 9);
            PrintData.Add("　　新規（Ｓｕｉｃａ定期券）");
            PrintData.Add(string.Format("　　　自社完結　　{0,6}枚　　　　 {1,9}円", num, amount));
            // 他社関連新規Ｓｕｉｃａ定期券クレジット売上枚数
            num = TotalizeTotalData.GetData(DataCountLabels.SeasonSuicaNewTotalOtherCreditSaleNum, 6);
            // 他社関連新規Ｓｕｉｃａ定期券クレジット売上金額
            amount = TotalizeTotalData.GetData(DataCountLabels.SeasonSuicaNewTotalOtherCreditSaleAmount, 9);
            PrintData.Add(string.Format("　　　他社関連　　{0,6}枚　　　　 {1,9}円", num, amount));
            // 空行
            PrintData.Add(string.Empty);

            // 自社完結継続Ｓｕｉｃａ定期券クレジット売上枚数
            num = TotalizeTotalData.GetData(DataCountLabels.SeasonSuicaContinueTotalOwnCreditSaleNum, 6);
            // 自社完結継続Ｓｕｉｃａ定期券クレジット売上金額
            amount = TotalizeTotalData.GetData(DataCountLabels.SeasonSuicaContinueTotalOwnCreditSaleAmount, 9);
            PrintData.Add("　　継続（Ｓｕｉｃａ定期券）");
            PrintData.Add(string.Format("　　　自社完結　　{0,6}枚　　　　 {1,9}円", num, amount));
            // 他社関連継続Ｓｕｉｃａ定期券クレジット売上枚数
            num = TotalizeTotalData.GetData(DataCountLabels.SeasonSuicaContinueTotalOtherCreditSaleNum, 6);
            // 他社関連継続Ｓｕｉｃａ定期券クレジット売上金額
            amount = TotalizeTotalData.GetData(DataCountLabels.SeasonSuicaContinueTotalOtherCreditSaleAmount, 9);
            PrintData.Add(string.Format("　　　他社関連　　{0,6}枚　　　　 {1,9}円", num, amount));
            // 空行
            PrintData.Add(string.Empty);

            // タイトル
            PrintData.Add("　現金廃札");
            // 自社完結新規磁気定期券現金廃札枚数
            num = TotalizeTotalData.GetData(DataCountLabels.SeasonNewTotalOwnCashRefundNum, 6);
            // 自社完結新規磁気定期券現金廃札金額
            amount = TotalizeTotalData.GetData(DataCountLabels.SeasonNewTotalOwnCashRefundAmount, 9);
            PrintData.Add("　　新規（磁気定期券）");
            PrintData.Add(string.Format("　　　自社完結　　{0,6}枚　　　　 {1,9}円", num, amount));
            // 他社関連新規磁気定期券現金廃札枚数
            num = TotalizeTotalData.GetData(DataCountLabels.SeasonNewTotalOtherCashRefundNum, 6);
            // 他社関連新規磁気定期券現金廃札金額
            amount = TotalizeTotalData.GetData(DataCountLabels.SeasonNewTotalOtherCashRefundAmount, 9);
            PrintData.Add(string.Format("　　　他社関連　　{0,6}枚　　　　 {1,9}円", num, amount));
            // 空行
            PrintData.Add(string.Empty);

            // 自社完結継続磁気定期券現金廃札枚数
            num = TotalizeTotalData.GetData(DataCountLabels.SeasonContinueTotalOwnCashRefundNum, 6);
            // 自社完結継続磁気定期券現金廃札金額
            amount = TotalizeTotalData.GetData(DataCountLabels.SeasonContinueTotalOwnCashRefundAmount, 9);
            PrintData.Add("　　継続（磁気定期券）");
            PrintData.Add(string.Format("　　　自社完結　　{0,6}枚　　　　 {1,9}円", num, amount));
            // 他社関連継続磁気定期券現金廃札枚数
            num = TotalizeTotalData.GetData(DataCountLabels.SeasonContinueTotalOtherCashRefundNum, 6);
            // 他社関連継続磁気定期券現金廃札金額
            amount = TotalizeTotalData.GetData(DataCountLabels.SeasonContinueTotalOtherCashRefundAmount, 9);
            PrintData.Add(string.Format("　　　他社関連　　{0,6}枚　　　　 {1,9}円", num, amount));
            // 空行
            PrintData.Add(string.Empty);

            // 自社完結新規Ｓｕｉｃａ定期券現金廃札枚数
            num = TotalizeTotalData.GetData(DataCountLabels.SeasonSuicaNewTotalOwnCashRefundNum, 6);
            // 自社完結新規Ｓｕｉｃａ定期券現金廃札金額
            amount = TotalizeTotalData.GetData(DataCountLabels.SeasonSuicaNewTotalOwnCashRefundAmount, 9);
            PrintData.Add("　　新規（Ｓｕｉｃａ定期券）");
            PrintData.Add(string.Format("　　　自社完結　　{0,6}枚　　　　 {1,9}円", num, amount));
            // 他社関連新規Ｓｕｉｃａ定期券現金廃札枚数
            num = TotalizeTotalData.GetData(DataCountLabels.SeasonSuicaNewTotalOtherCashRefundNum, 6);
            // 他社関連新規Ｓｕｉｃａ定期券現金廃札金額
            amount = TotalizeTotalData.GetData(DataCountLabels.SeasonSuicaNewTotalOtherCashRefundAmount, 9);
            PrintData.Add(string.Format("　　　他社関連　　{0,6}枚　　　　 {1,9}円", num, amount));
            // 空行
            PrintData.Add(string.Empty);

            // 自社完結継続Ｓｕｉｃａ定期券現金廃札枚数
            num = TotalizeTotalData.GetData(DataCountLabels.SeasonSuicaContinueTotalOwnCashRefundNum, 6);
            // 自社完結継続Ｓｕｉｃａ定期券現金廃札金額
            amount = TotalizeTotalData.GetData(DataCountLabels.SeasonSuicaContinueTotalOwnCashRefundAmount, 9);
            PrintData.Add("　　継続（Ｓｕｉｃａ定期券）");
            PrintData.Add(string.Format("　　　自社完結　　{0,6}枚　　　　 {1,9}円", num, amount));
            // 他社関連継続Ｓｕｉｃａ定期券現金廃札枚数
            num = TotalizeTotalData.GetData(DataCountLabels.SeasonSuicaContinueTotalOtherCashRefundNum, 6);
            // 他社関連継続Ｓｕｉｃａ定期券現金廃札金額
            amount = TotalizeTotalData.GetData(DataCountLabels.SeasonSuicaContinueTotalOtherCashRefundAmount, 9);
            PrintData.Add(string.Format("　　　他社関連　　{0,6}枚　　　　 {1,9}円", num, amount));
            // 空行
            PrintData.Add(string.Empty);

            // クレジット廃札
            PrintData.Add("　クレジット廃札");
            // 自社完結新規磁気定期券クレジット廃札枚数
            num = TotalizeTotalData.GetData(DataCountLabels.SeasonNewTotalOwnCreditRefundNum, 6);
            // 自社完結新規磁気定期券クレジット廃札金額
            amount = TotalizeTotalData.GetData(DataCountLabels.SeasonNewTotalOwnCreditRefundAmount, 9);
            PrintData.Add("　　新規（磁気定期券）");
            PrintData.Add(string.Format("　　　自社完結　　{0,6}枚　　　　 {1,9}円", num, amount));
            // 他社関連新規磁気定期券クレジット廃札枚数
            num = TotalizeTotalData.GetData(DataCountLabels.SeasonNewTotalOtherCreditRefundNum, 6);
            // 他社関連新規磁気定期券クレジット廃札金額
            amount = TotalizeTotalData.GetData(DataCountLabels.SeasonNewTotalOtherCreditRefundAmount, 9);
            PrintData.Add(string.Format("　　　他社関連　　{0,6}枚　　　　 {1,9}円", num, amount));
            // 空行
            PrintData.Add(string.Empty);

            // 自社完結継続磁気定期券クレジット廃札枚数
            num = TotalizeTotalData.GetData(DataCountLabels.SeasonContinueTotalOwnCreditRefundNum, 6);
            // 自社完結継続磁気定期券クレジット廃札金額
            amount = TotalizeTotalData.GetData(DataCountLabels.SeasonContinueTotalOwnCreditRefundAmount, 9);
            PrintData.Add("　　継続（磁気定期券）");
            PrintData.Add(string.Format("　　　自社完結　　{0,6}枚　　　　 {1,9}円", num, amount));
            // 他社関連継続磁気定期券クレジット廃札枚数
            num = TotalizeTotalData.GetData(DataCountLabels.SeasonContinueTotalOtherCreditRefundNum, 6);
            // 他社関連継続磁気定期券クレジット廃札金額
            amount = TotalizeTotalData.GetData(DataCountLabels.SeasonContinueTotalOtherCreditRefundAmount, 9);
            PrintData.Add(string.Format("　　　他社関連　　{0,6}枚　　　　 {1,9}円", num, amount));
            // 空行
            PrintData.Add(string.Empty);

            // 自社完結新規Ｓｕｉｃａ定期券クレジット廃札枚数
            num = TotalizeTotalData.GetData(DataCountLabels.SeasonSuicaNewTotalOwnCreditRefundNum, 6);
            // 自社完結新規Ｓｕｉｃａ定期券クレジット廃札金額
            amount = TotalizeTotalData.GetData(DataCountLabels.SeasonSuicaNewTotalOwnCreditRefundAmount, 9);
            PrintData.Add("　　新規（Ｓｕｉｃａ定期券）");
            PrintData.Add(string.Format("　　　自社完結　　{0,6}枚　　　　 {1,9}円", num, amount));
            // 他社関連新規Ｓｕｉｃａ定期券クレジット廃札枚数
            num = TotalizeTotalData.GetData(DataCountLabels.SeasonSuicaNewTotalOtherCreditRefundNum, 6);
            // 他社関連新規Ｓｕｉｃａ定期券クレジット廃札金額
            amount = TotalizeTotalData.GetData(DataCountLabels.SeasonSuicaNewTotalOtherCreditRefundAmount, 9);
            PrintData.Add(string.Format("　　　他社関連　　{0,6}枚　　　　 {1,9}円", num, amount));
            // 空行
            PrintData.Add(string.Empty);

            // 自社完結継続Ｓｕｉｃａ定期券クレジット廃札枚数
            num = TotalizeTotalData.GetData(DataCountLabels.SeasonSuicaContinueTotalOwnCreditRefundNum, 6);
            // 自社完結継続Ｓｕｉｃａ定期券クレジット廃札金額
            amount = TotalizeTotalData.GetData(DataCountLabels.SeasonSuicaContinueTotalOwnCreditRefundAmount, 9);
            PrintData.Add("　　継続（Ｓｕｉｃａ定期券）");
            PrintData.Add(string.Format("　　　自社完結　　{0,6}枚　　　　 {1,9}円", num, amount));
            // 他社関連継続Ｓｕｉｃａ定期券クレジット廃札枚数
            num = TotalizeTotalData.GetData(DataCountLabels.SeasonSuicaContinueTotalOtherCreditRefundNum, 6);
            // 他社関連継続Ｓｕｉｃａ定期券クレジット廃札金額
            amount = TotalizeTotalData.GetData(DataCountLabels.SeasonSuicaContinueTotalOtherCreditRefundAmount, 9);
            PrintData.Add(string.Format("　　　他社関連　　{0,6}枚　　　　 {1,9}円", num, amount));
            // 空行
            PrintData.Add(string.Empty);
        }

        /// <summary>
        /// チャージ
        /// </summary>
        private void JPDataEditForCharge()
        {
            // タイトル
            PrintData.Add("チャージ　＊＊＊＊＊＊＊＊＊＊＊＊＊＊＊＊＊＊＊");
            // 空行
            PrintData.Add(string.Empty);

            // チャージ現金売上件数
            long num = TotalizeTotalData.GetData(DataCountLabels.IccardTotalChargeNum, 6);
            // チャージ現金売上金額
            long amount = TotalizeTotalData.GetData(DataCountLabels.IccardTotalChargeAmount, 8);
            PrintData.Add(string.Format("　現金売上　　　　{0,6}件　　　　　{1,8}円", num, amount));
            // チャージクレジット売上件数
            num = TotalizeTotalData.GetData(DataCountLabels.IccardTotalCreditChargeNum, 6);
            // チャージクレジット売上金額
            amount = TotalizeTotalData.GetData(DataCountLabels.IccardTotalCreditChargeAmount, 8);
            PrintData.Add(string.Format("　クレジット売上　{0,6}件　　　　　{1,8}円", num, amount));
            // チャージ現金廃札件数
            num = TotalizeTotalData.GetData(DataCountLabels.IccardTotalChargeRefundNum, 6);
            // チャージ現金廃札金額
            amount = TotalizeTotalData.GetData(DataCountLabels.IccardTotalChargeRefundAmount, 8);
            PrintData.Add(string.Format("　現金廃札　　　　{0,6}件　　　　　{1,8}円", num, amount));
            // チャージクレジット廃札件数
            num = TotalizeTotalData.GetData(DataCountLabels.IccardTotalCreditChargeRefundNum, 6);
            // チャージクレジット廃札金額
            amount = TotalizeTotalData.GetData(DataCountLabels.IccardTotalCreditChargeRefundAmount, 8);
            PrintData.Add(string.Format("　クレジット廃札　{0,6}件　　　　　{1,8}円", num, amount));
            // 空行
            PrintData.Add(string.Empty);
        }

        /// <summary>
        /// デポジット徴収
        /// </summary>
        private void JPDataEditForDiposit()
        {
            // タイトル
            PrintData.Add("デポジット徴収　＊＊＊＊＊＊＊＊＊＊＊＊＊＊＊＊");
            // 空行
            PrintData.Add(string.Empty);

            // 現金徴収件数
            long num = TotalizeTotalData.GetData(DataCountLabels.IccardDipositTollNum, 6);
            // 現金徴収金額
            long amount = TotalizeTotalData.GetData(DataCountLabels.IccardDipositTollAmount, 8);
            PrintData.Add(string.Format("　現金徴収　　　　{0,6}件　　　　　{1,8}円", num, amount));
            // クレジット徴収件数
            num = TotalizeTotalData.GetData(DataCountLabels.IccardDipositCreditTollNum, 6);
            // クレジット徴収金額
            amount = TotalizeTotalData.GetData(DataCountLabels.IccardDipositCreditTollAmount, 8);
            PrintData.Add(string.Format("　クレジット徴収　{0,6}件　　　　　{1,8}円", num, amount));
            // 現金廃札件数
            num = TotalizeTotalData.GetData(DataCountLabels.IccardDipositRefundNum, 6);
            // 現金廃札金額
            amount = TotalizeTotalData.GetData(DataCountLabels.IccardDipositRefundAmount, 8);
            PrintData.Add(string.Format("　現金廃札　　　　{0,6}件　　　　　{1,8}円", num, amount));
            // クレジット廃札件数
            num = TotalizeTotalData.GetData(DataCountLabels.IccardDipositCreditRefundNum, 6);
            // クレジット廃札金額
            amount = TotalizeTotalData.GetData(DataCountLabels.IccardDipositCreditRefundAmount, 8);
            PrintData.Add(string.Format("　クレジット廃札　{0,6}件　　　　　{1,8}円", num, amount));
            // 空行
            PrintData.Add(string.Empty);
        }

        /// <summary>
        /// デポジット返却
        /// </summary>
        private void JPDataEditForDipositRest()
        {
            // タイトル
            PrintData.Add("デポジット返却　＊＊＊＊＊＊＊＊＊＊＊＊＊＊＊＊");
            // 空行
            PrintData.Add(string.Empty);

            // 現金返却件数
            long num = TotalizeTotalData.GetData(DataCountLabels.IccardTotalDipositRestNum, 6);
            // 現金返却金額
            long amount = TotalizeTotalData.GetData(DataCountLabels.IccardTotalDipositRestAmount, 8);
            PrintData.Add(string.Format("　現金返却　　　　{0,6}件　　　　　{1,8}円", num, amount));
            // 空行
            PrintData.Add(string.Empty);
        }

        /// <summary>
        /// Ｓｕｉｃａ
        /// </summary>
        private void JPDataEditForIC()
        {
            // タイトル
            PrintData.Add("Ｓｕｉｃａ　＊＊＊＊＊＊＊＊＊＊＊＊＊＊＊＊＊＊");
            // 空行
            PrintData.Add(string.Empty);

            // ホッバ操出枚数
            long num = TotalizeTotalData.GetData(DataCountLabels.SendOutCnt, 6);
            PrintData.Add(string.Format("　ホッバ操出枚数　　　　　　　{0,6}枚", num));
            // Ｓｕｉｃａ定期券
            num = TotalizeTotalData.GetData(DataCountLabels.SendOutSeasonSuicaCnt, 6);
            PrintData.Add(string.Format("　　Ｓｕｉｃａ定期券　　　　　{0,6}枚", num));
            // 無記名Ｓｕｉｃａ
            num = TotalizeTotalData.GetData(DataCountLabels.SendOutUnSignSuicaCnt, 6);
            PrintData.Add(string.Format("　　無記名Ｓｕｉｃａ　　　　　{0,6}枚", num));
            // 記名Ｓｕｉｃａ
            num = TotalizeTotalData.GetData(DataCountLabels.SendOutSignSuicaCnt, 6);
            PrintData.Add(string.Format("　　記名Ｓｕｉｃａ　　　　　　{0,6}枚", num));
            // 空行
            PrintData.Add(string.Empty);
            // タイトル
            PrintData.Add("　交換枚数");
            // 磁気→スイカ
            num = TotalizeTotalData.GetData(DataCountLabels.ChangeSeasonToSuicaCnt, 6);
            PrintData.Add(string.Format("　　　磁気　→スイカ　　　　　{0,6}枚", num));
            // スイカ→スイカ
            num = TotalizeTotalData.GetData(DataCountLabels.ChangeSuicaToSuicaCnt, 6);
            PrintData.Add(string.Format("　　スイカ　→スイカ　　　　　{0,6}枚", num));
            // スイカ→ビュー
            num = TotalizeTotalData.GetData(DataCountLabels.ChangeSuicaToViewCnt, 6);
            PrintData.Add(string.Format("　　スイカ　→ビュー　　　　　{0,6}枚", num));
            // ビュー→ビュー
            num = TotalizeTotalData.GetData(DataCountLabels.ViewSuicaToViewSuicaExchangeNum, 6);
            PrintData.Add(string.Format("　　ビュー　→ビュー　　　　　{0,6}枚", num));
            // 空行
            PrintData.Add(string.Empty);
        }

        /// <summary>
        /// 回収
        /// </summary>
        private void JPDataEditForCollect()
        {
            long num;
            // タイトル
            PrintData.Add("回収枚数　＊＊＊＊＊＊＊＊＊＊＊＊＊＊＊＊＊＊＊");
            // 空行
            PrintData.Add(string.Empty);
            // Ｓｕｉｃａイオ（券種３）枚数
            num = TotalizeTotalData.GetData(DataCountLabels.CollectSuicaIoCnt, 6);
            PrintData.Add(string.Format("　Ｓｕｉｃａイオ（券種３）　　{0,6}枚", num));
            // Ｓｕｉｃａカード（券種０）枚数
            num = TotalizeTotalData.GetData(DataCountLabels.CollectSuicaCnt, 6);
            PrintData.Add(string.Format("　Ｓｕｉｃａカード（券種０）　{0,6}枚", num));
            // 磁気定期券枚数
            num = TotalizeTotalData.GetData(DataCountLabels.CollectSeasonCnt, 6);
            PrintData.Add(string.Format("　磁気定期券　　　　　　　　　{0,6}枚", num));
            // 空行
            PrintData.Add(string.Empty);
        }

        /// <summary>
        /// 試刷
        /// </summary>
        private void JPDataEditForTest()
        {
            // タイトル
            PrintData.Add("試刷　＊＊＊＊＊＊＊＊＊＊＊＊＊＊＊＊＊＊＊＊＊");
            // 空行
            PrintData.Add(string.Empty);
            // きっぷ枚数
            long num = TotalizeTotalData.GetData(DataCountLabels.TicketTestprintNum, 6);
            PrintData.Add(string.Format("　きっぷ　　　　　　　　　　　{0,6}枚", num));
            // 磁気定期券枚数
            num = TotalizeTotalData.GetData(DataCountLabels.SeasonTestprintNum, 6);
            PrintData.Add(string.Format("　磁気定期券　　　　　　　　　{0,6}枚", num));
            // 空行
            PrintData.Add(string.Empty);
        }

        /// <summary>
        /// 金庫枚数
        /// </summary>
        private void JPDataEditForCoffer()
        {
            // タイトル
            PrintData.Add("金庫枚数　＊＊＊＊＊＊＊＊＊＊＊＊＊＊＊＊＊＊＊");
            // 空行
            PrintData.Add(string.Empty);

            // １０円
            long num = TotalizeTotalData.GetData(DataCountLabels.Coffer10Num, 6);
            PrintData.Add(string.Format("　　　10円　　　　　　　　　　{0,6}枚", num));
            // ５０円
            num = TotalizeTotalData.GetData(DataCountLabels.Coffer50Num, 6);
            PrintData.Add(string.Format("　　　50円　　　　　　　　　　{0,6}枚", num));
            // １００円
            num = TotalizeTotalData.GetData(DataCountLabels.Coffer100Num, 6);
            PrintData.Add(string.Format("　　 100円　　　　　　　　　　{0,6}枚", num));
            // ５００円
            num = TotalizeTotalData.GetData(DataCountLabels.Coffer500Num, 6);
            PrintData.Add(string.Format("　　 500円　　　　　　　　　　{0,6}枚", num));
            // １０００円
            num = TotalizeTotalData.GetData(DataCountLabels.Coffer1000Num, 6);
            PrintData.Add(string.Format("　　1000円　　　　　　　　　　{0,6}枚", num));
            // ２０００円
            num = TotalizeTotalData.GetData(DataCountLabels.Coffer2000Num, 6);
            PrintData.Add(string.Format("　　2000円　　　　　　　　　　{0,6}枚", num));
            // ５０００円
            num = TotalizeTotalData.GetData(DataCountLabels.Coffer5000Num, 6);
            PrintData.Add(string.Format("　　5000円　　　　　　　　　　{0,6}枚", num));
            // １００００円
            num = TotalizeTotalData.GetData(DataCountLabels.Coffer10000Num, 6);
            PrintData.Add(string.Format("　 10000円　　　　　　　　　　{0,6}枚", num));
            // 空行
            PrintData.Add(string.Empty);
        }

        /// <summary>
        /// 単発枚数
        /// </summary>
        private void JPDataEditForSingle()
        {
            // タイトル
            PrintData.Add("単発枚数　＊＊＊＊＊＊＊＊＊＊＊＊＊＊＊＊＊＊＊");
            // 空行
            PrintData.Add(string.Empty);

            // １０円
            long num = TotalizeTotalData.GetData(DataCountLabels.TotalSingle10Num, 6);
            PrintData.Add(string.Format("　　　10円　　　　　　　　　　{0,6}枚", num));
            // ５０円
            num = TotalizeTotalData.GetData(DataCountLabels.TotalSingle50Num, 6);
            PrintData.Add(string.Format("　　　50円　　　　　　　　　　{0,6}枚", num));
            // １００円
            num = TotalizeTotalData.GetData(DataCountLabels.TotalSingle100Num, 6);
            PrintData.Add(string.Format("　　 100円　　　　　　　　　　{0,6}枚", num));
            // ５００円
            num = TotalizeTotalData.GetData(DataCountLabels.TotalSingle500Num, 6);
            PrintData.Add(string.Format("　　 500円　　　　　　　　　　{0,6}枚", num));
            // １０００円
            num = TotalizeTotalData.GetData(DataCountLabels.TotalSingle1000Num, 6);
            PrintData.Add(string.Format("　　1000円　　　　　　　　　　{0,6}枚", num));
            // 空行
            PrintData.Add(string.Empty);
        }

        /// <summary>
        /// 取忘
        /// </summary>
        private void JPDataEditForForget()
        {
            // タイトル
            PrintData.Add("取忘　＊＊＊＊＊＊＊＊＊＊＊＊＊＊＊＊＊＊＊＊＊");
            // 空行
            PrintData.Add(string.Empty);
            // 乗車券
            long num = TotalizeTotalData.GetData(DataCountLabels.ForgetTicketCnt, 6);
            PrintData.Add(string.Format("　乗車券　　　　　　　　　　　{0,6}枚", num));
            // 領収証
            num = TotalizeTotalData.GetData(DataCountLabels.ForgetReceiptCnt, 6);
            PrintData.Add(string.Format("　領収証　　　　　　　　　　　{0,6}枚", num));
            // 履歴
            num = TotalizeTotalData.GetData(DataCountLabels.ForgetHistoryCnt, 6);
            PrintData.Add(string.Format("　履歴　　　　　　　　　　　　{0,6}枚", num));
            // カード
            num = TotalizeTotalData.GetData(DataCountLabels.ForgetCardCnt, 6);
            PrintData.Add(string.Format("　カード　　　　　　　　　　　{0,6}枚", num));
            // 磁気定期券
            num = TotalizeTotalData.GetData(DataCountLabels.ForgetSeasonCnt, 6);
            PrintData.Add(string.Format("　磁気定期券　　　　　　　　　{0,6}枚", num));
            // ＩＣＳＦ
            num = TotalizeTotalData.GetData(DataCountLabels.ForgetIoSuicaCnt, 6);
            PrintData.Add(string.Format("　ＩＣＳＦ　　　　　　　　　　{0,6}枚", num));
            // ＩＣ定期券
            num = TotalizeTotalData.GetData(DataCountLabels.ForgetSeasonSuicaCnt, 6);
            PrintData.Add(string.Format("　ＩＣ定期券　　　　　　　　　{0,6}枚", num));
            // 紙幣　千円
            num = TotalizeTotalData.GetData(DataCountLabels.ForgetBill1000Cnt, 6);
            PrintData.Add(string.Format("　紙幣　千円　　　　　　　　　{0,6}枚", num));
            // 紙幣　２千円
            num = TotalizeTotalData.GetData(DataCountLabels.ForgetBill2000Cnt, 6);
            PrintData.Add(string.Format("　　　２千円　　　　　　　　　{0,6}枚", num));
            // 紙幣　５千円
            num = TotalizeTotalData.GetData(DataCountLabels.ForgetBill5000Cnt, 6);
            PrintData.Add(string.Format("　　　５千円　　　　　　　　　{0,6}枚", num));
            // 未使用証
            num = TotalizeTotalData.GetData(DataCountLabels.ForgetCertificationCnt, 6);
            PrintData.Add(string.Format("　未使用証　　　　　　　　　　{0,6}枚", num));
            // 利用明細
            num = TotalizeTotalData.GetData(DataCountLabels.ForgetCreditJournal, 6);
            PrintData.Add(string.Format("　ご利用明細　　　　　　　　　{0,6}枚", num));
            // 空行
            PrintData.Add(string.Empty);
        }

        /// <summary>
        /// ＳＦ返金
        /// </summary>
        private void JPDataEditForSfback()
        {
            // タイトル
            PrintData.Add("ＳＦ返金　＊＊＊＊＊＊＊＊＊＊＊＊＊＊＊＊＊＊＊");
            // 空行
            PrintData.Add(string.Empty);
            // ＳＦ返金件数
            long num = TotalizeTotalData.GetData(DataCountLabels.IccardTotalSfbackNum, 6);
            // ＳＦ返金金額
            long amount = TotalizeTotalData.GetData(DataCountLabels.IccardTotalSfbackAmount, 8);
            PrintData.Add(string.Format("　返金　　　　　　{0,6}件　　　　　{1,8}円", num, amount));
            // 空行
            PrintData.Add(string.Empty);
        }

        /// <summary>
        /// ポイントチャージ
        /// </summary>
        private void JPDataEditForPoint()
        {
            // タイトル
            PrintData.Add("ポイントチャージ　＊＊＊＊＊＊＊＊＊＊＊＊＊＊＊");
            // 空行
            PrintData.Add(string.Empty);
            // ポイントチャージ件数
            long num = TotalizeTotalData.GetData(DataCountLabels.IccardTotalPchargeNum, 6);
            long amount = TotalizeTotalData.GetData(DataCountLabels.IccardTotalPchargeAmount, 8);
            PrintData.Add(string.Format("　還元　　　　　　{0,6}件　　　　　{1,8}円", num, amount));
        }

        /// <summary>
        /// タイトル、管理日計日、設置駅コードおよびコーナ、号機（多機能券売機用）
        /// </summary>
        private void JPDataEditForViewScrapTitle()
        {
            PrintData.Add("－－－－－－－－－－－－－－－－－－－－－－－－");
            PrintData.Add("＊＊＊＊＊　ビュースイカ廃札記録　＊＊＊＊＊＊＊");
            // 日付
            System.DateTime currentTime = System.DateTime.Now;
            string printData = string.Format("　　　　　　　　　　　　{0:D4}年　{1:D2}月　{2:D2}日", currentTime.Year, currentTime.Month, currentTime.Day);
            PrintData.Add(printData);
            // 設置駅コード、号機
            string machineNo = DataStore.GetInstance().MaintenanceSettings.PrintNo.Value;
            PrintData.Add(string.Format("　　設置駅コード　{0:D6}　　　　　　　{1,2}号機", Convert.ToInt32(settings.HostCode.Value), Convert.ToInt32(machineNo)));
            // 空行
            PrintData.Add(string.Empty);
            PrintData.Add(string.Empty);
        }

        /// <summary>
        /// 廃札ＩＤｉ作成
        /// </summary>
        /// <returns>ＩＤｉ</returns>
        private List<string> JPDataEditForScrapIdi()
        {
            // タイトル
            PrintData.Add("　　廃札ＩＤi");
            var idiData = new List<string>();
            if (TotalizeDataCount.GetInstance().GetScrapIDi(DataCountBase.Generation.Old1, TotalizeDataCount.TotalizeKind.Total, ref idiData))
            {
                foreach (string data in idiData)
                {
                    // 廃札ＩＤi
                    string printData = Strings.StrConv(data, VbStrConv.Wide);
                    PrintData.Add("　　　" + printData);
                }
            }
            PrintData.Add("－－－－－－－－－－－－－－－－－－－－－－－－");

            // 空行
            PrintData.Add(string.Empty);
            return idiData;
        }

        /// <summary>
        /// ジャーナル印字用の支払方法を編集
        /// </summary>
        private void JPDataEditForPayment()
        {
            if ((session.Payment.Value == Session.PaymentType.Credit) ||
                (session.Payment.Value == Session.PaymentType.ViewCard))
            {
                // クレジット
                PrintData.Add("支払方法　" + session.CreditInfo.Value.CreditCompanyName);
            }
            else
            {
                // 現金
                PrintData.Add("支払方法　現金");
            }
        }

        /// <summary>
        /// ジャーナル印字用の定期券の３行目（券番号、購入金額）を編集
        /// </summary>
        /// <param name="number">券番号</param>
        /// <param name="iserr">発券前異常フラグ</param>
        /// <returns>券番号有無</returns>
        private bool JPDataEditForPassLine3(ref long number, bool iserr = false)
        {
            long purchase = 0;
            bool exist = true;

            purchase = session.PurchaseAmount.Value;
            string amount = Commamodify(purchase);
            if (!iserr)
            {
                // 正常発売
                SessionSaveTable sessionSaveTable = new SessionSaveTable();
                session.GetSessionDataStore(sessionSaveTable);
                bool newMedia = false;
                bool updateMedia = false;
                sessionSaveTable.GetICMedia(ref newMedia, ref updateMedia);

                if (newMedia)
                {
                    // 新規カード有
                    number = session.IssueIcCardInfo.Values.Find(info => info.IsUpdate).PassSerialNumber;
                }
                else
                {
                    // 新規カード無
                    if (updateMedia)
                    {
                        if (!session.ReadCardList[0].SeasonPassInfo)
                        {
                            // 定期情報がある
                            number = session.PassInfo.Value.TicketNumber;
                        }
                        else
                        {
                            // 定期情報がなし
                            exist = false;
                        }
                    }
                    else
                    {
                        if (!session.ReadCardList[1].SeasonPassInfo)
                        {
                            // 定期情報がある
                            number = session.PassInfo.Value.TicketNumber;
                        }
                        else
                        {
                            // 定期情報がなし
                            exist = false;
                        }
                    }
                }
                if (exist)
                {
                    PrintData.Add(string.Format("　券番号　{0:D5} 　　　購入金額　　　", number) + amount);
                }
                else
                {
                    PrintData.Add(string.Format("　　　　　　　　　　　購入金額　　　" + amount));
                }
            }
            else
            {
                // 印刷開始前発券異常
                PrintData.Add(string.Format("　券番号　　　　　　　購入金額　　　" + amount));
                exist = false;
            }

            return exist;
        }

        /// <summary>
        /// ジャーナル印字用の定期券の４行目（種別、定期券金額）を編集
        /// </summary>
        private void JPDataEditForPassLine4()
        {
            string purchase = string.Empty;
            string type = string.Empty;
            string printdata = string.Empty;
            switch (session.PassInfo.Value.ChoiceProcess)
            {
                case ChoiceProcessType.New:
                    type = "　種別　　新規　　　　　";
                    break;
                case ChoiceProcessType.Continue:
                    type = "　種別　　継続　　　　　";
                    break;
                case ChoiceProcessType.TakeOver:
                    type = "　種別　　引継継続　　　";
                    break;
                default:
                    type = "　　　　　　　　　　　　";
                    break;
            }
            purchase = Commamodify(session.SeasonAmount.Value);
            printdata = type + "定期券　　　" + purchase;
            PrintData.Add(printdata);
        }

        /// <summary>
        /// ジャーナル印字用の定期券の５行目（媒体、チャージ金額）を編集
        /// </summary>
        private void JPDataEditForPassLine5()
        {
            string purchase = string.Empty;
            string type = string.Empty;
            string printdata = string.Empty;
            switch (session.PassInfo.Value.IssueMedia)
            {
                case IssueMediaType.IcPassExist:
                case IssueMediaType.IcPassNew:
                    type = "　媒体　　Ｓｕｉｃａ　　";
                    break;
                case IssueMediaType.Pass:
                    type = "　媒体　　磁気　　　　　";
                    break;
                default:
                    type = "　　　　　　　　　　　　";
                    break;
            }
            purchase = Commamodify(session.ChargeAmount.Value);
            printdata = type + "チャージ　　" + purchase;
            PrintData.Add(printdata);
        }

        /// <summary>
        /// ジャーナル印字用の定期券の６行目（券種、デポジット）を編集
        /// </summary>
        private void JPDataEditForPassLine6()
        {
            string type = string.Empty;
            string printdata = string.Empty;
            if (session.Operation.Value == Session.OperationType.Pass)
            {
                switch (session.PassInfo.Value.Attend)
                {
                    case AttendType.Work:
                        type = "　券種　　通勤　　　　　";
                        break;
                    case AttendType.University:
                    case AttendType.Training:
                    case AttendType.HighSchool:
                    case AttendType.JuniorHigh:
                    case AttendType.Elementary:
                    case AttendType.Student:
                        type = "　券種　　通学　　　　　";
                        break;
                    case AttendType.Green:
                        type = "　券種　　グリーン　　　";
                        break;
                    case AttendType.Frex:
                        type = "　券種　　ＦＲＥＸ　　　";
                        break;
                    case AttendType.FrexPal:
                        type = "　券種　　FREXパル　　　";
                        break;
                    default:
                        type = "　　　　　　　　　　　　";
                        break;
                }
            }
            else
            {
                type = "　券種　　ＩＣＳＦ　　　";
            }
            string purchase = Commamodify(session.DepositAmount.Value);
            printdata = type + "デポジット　" + purchase;
            PrintData.Add(printdata);
        }

        /// <summary>
        /// ジャーナル印字用の定期券の７行目（大小、投入金額）を編集
        /// </summary>
        private void JPDataEditForPassLine7()
        {
            string amount = string.Empty;
            string type = string.Empty;
            string printdata = string.Empty;
            if (session.PersonalInfo.Value.AgeGroup == ReadCardInfo.AdultChildType.NormalChild)
            {
                type = "　大小　　こども　　　";
            }
            else
            {
                type = "　大小　　大人　　　　";
            }
            if (session.Payment.Value == Session.PaymentType.Credit ||
                session.Payment.Value == Session.PaymentType.ViewCard)
            {
                printdata = type;
                PrintData.Add(printdata);
            }
            else
            {
                amount = Commamodify(session.ActualPooledTotal.Value);
                printdata = type + "投入金額　　　" + amount;
                PrintData.Add(printdata);
            }
        }

        /// <summary>
        /// ジャーナル印字用の定期券の８行目（割引、おつり）を編集
        /// </summary>
        private void JPDataEditForPassLine8()
        {
            string amount = string.Empty;
            string type = string.Empty;
            string printdata = string.Empty;
            switch (session.PassInfo.Value.Attend)
            {
                case AttendType.Elementary:
                    type = "　割引　　小学生　　　";
                    break;
                case AttendType.JuniorHigh:
                    type = "　割引　　中学生　　　";
                    break;
                case AttendType.HighSchool:
                    type = "　割引　　高校生　　　";
                    break;
                case AttendType.Training:
                    type = "　割引　　養成訓練生　";
                    break;
                default:
                    type = "　　　　　　　　　　　";
                    break;
            }
            if (session.Payment.Value == Session.PaymentType.Credit ||
                session.Payment.Value == Session.PaymentType.ViewCard)
            {
                if (type != "　　　　　　　　　　　")
                {
                    PrintData.Add(type);
                }
            }
            else
            {
                amount = Commamodify(session.LogicalChangeBills.Value.Total + session.LogicalChangeCoins.Value.Total);
                printdata = type + "おつり　　　　" + amount;
                PrintData.Add(printdata);
            }

            // 空行
            PrintData.Add(string.Empty);
        }

        /// <summary>
        /// ジャーナル印字用のカードのＩＤｉ（発行カード、旧カード）を編集
        /// </summary>
        /// <param name="newno">新カードＩｄｉ</param>
        /// <param name="oldno">旧カードＩｄｉ</param>
        private void JPDataEditForIDi(ref string newno, ref string oldno)
        {
            string printdata = string.Empty;
            SessionSaveTable sessionSaveTable = new SessionSaveTable();
            session.GetSessionDataStore(sessionSaveTable);
            bool newMedia = false;
            bool updateMedia = false;
            sessionSaveTable.GetICMedia(ref newMedia, ref updateMedia);

            // 発行カード
            if (newMedia)
            {
                if (string.IsNullOrEmpty(session.IssueIcCardInfo.Values.Find(info => info.IsUpdate).PrintIdi) == false)
                {
                    printdata = "ＩＤｉ　　" + Strings.StrConv(session.IssueIcCardInfo.Values.Find(info => info.IsUpdate).PrintIdi, VbStrConv.Wide);
                    PrintData.Add(printdata);
                    newno = session.IssueIcCardInfo.Values.Find(info => info.IsUpdate).PrintIdi;
                    if (updateMedia && (session.ReadCardList.Values.Count > 0))
                    {
                        if (string.IsNullOrEmpty(session.ReadCardList[0].PrintIdi) == false)
                        {
                            printdata = "旧ＩＤｉ　" + Strings.StrConv(session.ReadCardList[0].PrintIdi, VbStrConv.Wide);
                            PrintData.Add(printdata);
                            oldno = session.ReadCardList[0].PrintIdi;
                        }
                    }
                    else if (!updateMedia && (session.ReadCardList.Values.Count > 1))
                    {
                        if (string.IsNullOrEmpty(session.ReadCardList[1].PrintIdi) == false)
                        {
                            printdata = "旧ＩＤｉ　" + Strings.StrConv(session.ReadCardList[1].PrintIdi, VbStrConv.Wide);
                            PrintData.Add(printdata);
                            oldno = session.ReadCardList[1].PrintIdi;
                        }
                    }
                    // 空行
                    PrintData.Add(string.Empty);
                }
            }
            else
            {
                if (updateMedia && (session.ReadCardList.Values.Count > 0))
                {
                    if (string.IsNullOrEmpty(session.ReadCardList[0].PrintIdi) == false)
                    {
                        printdata = "ＩＤｉ　　" + Strings.StrConv(session.ReadCardList[0].PrintIdi, VbStrConv.Wide);
                        PrintData.Add(printdata);
                        // 空行
                        PrintData.Add(string.Empty);
                        oldno = session.ReadCardList[0].PrintIdi;
                    }
                }
                else if (!updateMedia && (session.ReadCardList.Values.Count > 1))
                {
                    if (string.IsNullOrEmpty(session.ReadCardList[1].PrintIdi) == false)
                    {
                        printdata = "ＩＤｉ　　" + Strings.StrConv(session.ReadCardList[1].PrintIdi, VbStrConv.Wide);
                        PrintData.Add(printdata);
                        // 空行
                        PrintData.Add(string.Empty);
                        oldno = session.ReadCardList[1].PrintIdi;
                    }
                }
            }
        }

        /// <summary>
        /// ジャーナル印字用の個人データを編集
        /// </summary>
        /// <param name="ispass">定期券フラグ</param>
        private void JPDataEditForPersonal(bool ispass = false)
        {
            string printdata = string.Empty;
            DateInputCLR birthday = new DateInputCLR();
            int age = 0;
            bool newMedia = false;
            bool updateMedia = false;
            SessionSaveTable sessionSaveTable = new SessionSaveTable();
            session.GetSessionDataStore(sessionSaveTable);
            sessionSaveTable.GetICMedia(ref newMedia, ref updateMedia);

            if (newMedia)
            {
                if (session.Operation.Value != Session.OperationType.UnsignIc)
                {
                    if (session.PersonalInfo.Value.Sex == SexType.Male)
                    {
                        printdata = "氏名　　　" + session.PersonalInfo.Value.NameKana.PadRight(16) + "　　性別　男";
                    }
                    else
                    {
                        printdata = "氏名　　　" + session.PersonalInfo.Value.NameKana.PadRight(16) + "　　性別　女";
                    }
                    // 氏名、性別
                    PrintData.Add(printdata);
                    // 生年月日
                    birthday = session.PersonalInfo.Value.BirthDay;
                    // 年齢
                    age = session.PersonalInfo.Value.Age;
                    PrintData.Add(string.Format("生年月日　{0:D4}年{1:D2}月{2:D2}日　　　年齢　{3,2}オ", birthday.Year, birthday.Month, birthday.Day, age));
                    // 電話番号
                    PrintData.Add("電話番号　" + Strings.StrConv(session.PersonalInfo.Value.Phone, VbStrConv.Narrow));
                    PrintData.Add(string.Empty);
                }
            }
            else
            {
                if (updateMedia)
                {
                    if (session.ReadCardList[0].Sign)
                    {
                        if (session.PersonalInfo.Value.Sex == SexType.Male)
                        {
                            printdata = "氏名　　　" + session.PersonalInfo.Value.NameKana.PadRight(16) + "　　性別　男";
                        }
                        else
                        {
                            printdata = "氏名　　　" + session.PersonalInfo.Value.NameKana.PadRight(16) + "　　性別　女";
                        }
                        // 氏名、性別
                        PrintData.Add(printdata);
                        // 生年月日
                        birthday = session.PersonalInfo.Value.BirthDay;
                        // 年齢
                        age = session.PersonalInfo.Value.Age;
                        PrintData.Add(string.Format("生年月日　{0:D4}年{1:D2}月{2:D2}日　　　年齢　{3,2}オ", birthday.Year, birthday.Month, birthday.Day, age));
                        // 電話番号
                        PrintData.Add("電話番号　" + session.PersonalInfo.Value.Phone);
                        PrintData.Add(string.Empty);
                    }
                }
                else
                {
                    if (session.ReadCardList[1].Sign)
                    {
                        if (session.PersonalInfo.Value.Sex == SexType.Male)
                        {
                            printdata = "氏名　　　" + session.PersonalInfo.Value.NameKana.PadRight(16) + "　　性別　男";
                        }
                        else
                        {
                            printdata = "氏名　　　" + session.PersonalInfo.Value.NameKana.PadRight(16) + "　　性別　女";
                        }
                        // 氏名、性別
                        PrintData.Add(printdata);
                        // 生年月日
                        birthday = session.PersonalInfo.Value.BirthDay;
                        // 年齢
                        age = session.PersonalInfo.Value.Age;
                        PrintData.Add(string.Format("生年月日　{0:D4}年{1:D2}月{2:D2}日　　　年齢　{3,2}オ", birthday.Year, birthday.Month, birthday.Day, age));
                        // 電話番号
                        PrintData.Add("電話番号　" + session.PersonalInfo.Value.Phone);
                        PrintData.Add(string.Empty);
                    }
                }
            }
        }

        /// <summary>
        /// ジャーナル印字用の定期券期間データを編集
        /// </summary>
        private void JPDataEditForPassTerm()
        {
            DateInputCLR useday = new DateInputCLR();
            DateInputCLR startday = new DateInputCLR();
            DateInputCLR endday = new DateInputCLR();
            string term = string.Empty;
            string printdata = string.Empty;

            // 購入期間
            switch (session.PassInfo.Value.Term)
            {
                case TermType.OneMonth:
                    term = "購入期間　１箇月　";
                    break;
                case TermType.ThreeMonths:
                    term = "購入期間　３箇月　";
                    break;
                case TermType.SixMonths:
                    term = "購入期間　６箇月　";
                    break;
                default:
                    term = "　　　　　　　　　";
                    break;
            }
            // 使用開始日
            useday = session.PassInfo.Value.UseStartDate;
            PrintData.Add(term + string.Format("使用開始日　{0:D2}年{1:D2}月{2:D2}日から", useday.Year % 100, useday.Month, useday.Day));
            // 有効開始日
            startday = session.PassInfo.Value.StartDate;
            // 有効終了日
            endday = session.PassInfo.Value.EndDate;
            if (session.PassInfo.Value.ControlDay == 0)
            {
                printdata = string.Format("有効期間　{0:D2}年{1:D2}月{2:D2}日～{3:D2}年{4:D2}月{5:D2}日", startday.Year % 100, startday.Month, startday.Day, endday.Year % 100, endday.Month, endday.Day);
            }
            else
            {
                printdata = string.Format("有効期間　{0:D2}年{1:D2}月{2:D2}日～{3:D2}年{4:D2}月{5:D2}日　日調{6:D2}", startday.Year % 100, startday.Month, startday.Day, endday.Year % 100, endday.Month, endday.Day, (int)session.PassInfo.Value.ControlDay);
            }
            PrintData.Add(printdata);
            PrintData.Add(string.Empty);
        }

        /// <summary>
        /// ジャーナル印字用の定期券範囲データを編集
        /// </summary>
        private void JPDataEditForPassRange()
        {
            // 定期券１区間
            JPDataEditForPassOneSection();
            // 定期券２区間
            if (session.PassInfo.Value.TwoSection)
            {
                JPDataEditForPassOneSection(true);
            }
        }

        /// <summary>
        /// ジャーナル印字用の定期券１,2区間範囲データを編集
        /// </summary>
        /// <param name="isTwoSection">２区間ですか　True：２区間　False：１区間</param>
        private void JPDataEditForPassOneSection(bool isTwoSection = false)
        {
            string printData = string.Empty;
            string printData1 = string.Empty;
            IcoreTeikiKukanInfo teikiKukanInfo = new IcoreTeikiKukanInfo();
            // タイル
            if (isTwoSection)
            {
                PrintData.Add("２区間目");
                teikiKukanInfo = session.PassInfo.Value.TeikiKukanInfo[1];
            }
            else
            {
                PrintData.Add("＜発行定期券＞");
                teikiKukanInfo = session.PassInfo.Value.TeikiKukanInfo[0];
            }
            // 発駅
            GetStationName(teikiKukanInfo, StationType.HatsuCode, out printData);
            PrintData.Add(string.Format("発駅　　　　　{0}", printData));
            // 着駅
            GetStationName(teikiKukanInfo, StationType.TyakuCode, out printData);
            PrintData.Add(string.Format("着駅　　　　　{0}", printData));
            // グリーン
            if (GetStationName(teikiKukanInfo, StationType.GreenHatsuCode, out printData))
            {
                // グリーン発駅
                PrintData.Add(string.Format("グリーン発駅　{0}", printData));
                // グリーン着駅
                GetStationName(teikiKukanInfo, StationType.GreenTyakuCode, out printData1);
                PrintData.Add(string.Format("グリーン着駅　{0}", printData1));
                // グリーン経由
                GetStationName(teikiKukanInfo, StationType.ExpressKeiyu, out printData);
                PrintData.Add(string.Format("グリーン経由　{0}", printData));
            }
            // 新幹線
            if (GetStationName(teikiKukanInfo, StationType.ShinkansenHatsuCode, out printData))
            {
                // 新幹線発駅
                PrintData.Add(string.Format("新幹線発駅　　{0}", printData));
                // 新幹線着駅
                GetStationName(teikiKukanInfo, StationType.ShinkansenTyakuCode, out printData1);
                PrintData.Add(string.Format("新幹線着駅　　{0}", printData1));
                // 新幹線経由
                GetStationName(teikiKukanInfo, StationType.ExpressKeiyu, out printData);
                PrintData.Add(string.Format("新幹線経由　　{0}", printData));
            }

            // 空行
            PrintData.Add(string.Empty);
            // 経由１～10
            int i = 1;
            for (int num = 1; num <= 10; num++)
            {
                if (GetStationName(teikiKukanInfo.KeiyuCode[num - 1], out printData))
                {
                    if (i < 10)
                    {
                        PrintData.Add(string.Format("経由{0}　　　　{1}", Strings.StrConv(i.ToString(), VbStrConv.Wide), printData));
                    }
                    else
                    {
                        PrintData.Add(string.Format("経由{0,2}　　　　{1}", i, printData));
                    }
                    i++;
                }
            }
            if (i != 1)
            {
                PrintData.Add(string.Empty);
            }
        }

        /// <summary>
        /// 経由駅コードによって、駅名を取得
        /// </summary>
        /// <param name="KeiyuCode">経由情報</param>
        /// <param name="Text">編集用駅名</param>
        /// <returns>経由有無状態、True：あり　False：なし</returns>
        private bool GetStationName(int KeiyuCode, out string Text)
        {
            bool ret = false;
            var operationalDate = new OperationalDateTime();
            Text = UtilityInterface.GetStationInfo((byte)((KeiyuCode >> 8) & 0xFF), (byte)(KeiyuCode & 0xFF), UtilityInterface.StationNameFullReceiptJpn, operationalDate.GetThreeHourDate());
            if (!string.IsNullOrWhiteSpace(Text))
            {
                ret = true;
            }
            return ret;
        }

        /// <summary>
        /// 駅コードによって、駅名を取得
        /// </summary>
        /// <param name="StationInfo">区間情報</param>
        /// <param name="stationType">駅種別</param>
        /// <param name="Text">編集用駅名</param>
        /// <returns>駅有無状態、True：あり　False：なし</returns>
        private bool GetStationName(IcoreTeikiKukanInfo StationInfo, StationType stationType, out string Text)
        {
            bool ret = false;
            var operationalDate = new OperationalDateTime();
            switch (stationType)
            {
                case StationType.HatsuCode:
                    Text = UtilityInterface.GetStationInfo((byte)((StationInfo.HatsuCode >> 8) & 0xFF), (byte)(StationInfo.HatsuCode & 0xFF), UtilityInterface.StationNameFullReceiptJpn, operationalDate.GetThreeHourDate());
                    break;
                case StationType.TyakuCode:
                    Text = UtilityInterface.GetStationInfo((byte)((StationInfo.TyakuCode >> 8) & 0xFF), (byte)(StationInfo.TyakuCode & 0xFF), UtilityInterface.StationNameFullReceiptJpn, operationalDate.GetThreeHourDate());
                    break;
                case StationType.GreenHatsuCode:
                    Text = UtilityInterface.GetStationInfo((byte)((StationInfo.GreenHatsuCode >> 8) & 0xFF), (byte)(StationInfo.GreenHatsuCode & 0xFF), UtilityInterface.StationNameFullReceiptJpn, operationalDate.GetThreeHourDate());
                    break;
                case StationType.GreenTyakuCode:
                    Text = UtilityInterface.GetStationInfo((byte)((StationInfo.GreenTyakuCode >> 8) & 0xFF), (byte)(StationInfo.GreenTyakuCode & 0xFF), UtilityInterface.StationNameFullReceiptJpn, operationalDate.GetThreeHourDate());
                    break;
                case StationType.ShinkansenHatsuCode:
                    Text = UtilityInterface.GetStationInfo((byte)((StationInfo.ShinkansenHatsuCode >> 8) & 0xFF), (byte)(StationInfo.ShinkansenHatsuCode & 0xFF), UtilityInterface.StationNameFullReceiptJpn, operationalDate.GetThreeHourDate());
                    break;
                case StationType.ShinkansenTyakuCode:
                    Text = UtilityInterface.GetStationInfo((byte)((StationInfo.ShinkansenTyakuCode >> 8) & 0xFF), (byte)(StationInfo.ShinkansenTyakuCode & 0xFF), UtilityInterface.StationNameFullReceiptJpn, operationalDate.GetThreeHourDate());
                    break;
                case StationType.ExpressKeiyu:
                    Text = UtilityInterface.GetStationInfo((byte)((StationInfo.ExpressKeiyu >> 8) & 0xFF), (byte)(StationInfo.ExpressKeiyu & 0xFF), UtilityInterface.StationNameFullReceiptJpn, operationalDate.GetThreeHourDate());
                    break;
                default:
                    Text = string.Empty;
                    break;
            }
            if (!string.IsNullOrWhiteSpace(Text))
            {
                ret = true;
            }
            return ret;
        }

        /// <summary>
        /// ジャーナル印字用のSuica発行の３行目（種別、購入金額）を編集
        /// </summary>
        private void JPDataEditForCardIssueLine3()
        {
            long purchase = 0;
            purchase = session.PurchaseAmount.Value;
            string amount = Commamodify(purchase);
            PrintData.Add("　種別　　新規　　　　購入金額　　　" + amount);
        }

        /// <summary>
        /// ジャーナル印字用のSuica発行の４行目（媒体、チャージ金額）を編集
        /// </summary>
        private void JPDataEditForCardIssueLine4()
        {
            string purchase = string.Empty;
            string type = "　媒体　　Ｓｕｉｃａ　　";
            string printdata = string.Empty;
            purchase = Commamodify(session.ChargeAmount.Value);
            printdata = type + "チャージ　　" + purchase;
            PrintData.Add(printdata);
        }

        /// <summary>
        /// ジャーナル印字用の交換データを編集
        /// </summary>
        /// <param name="kind">種別</param>
        /// <param name="number">券番号</param>
        /// <param name="iserr">結果電文フラグ</param>
        /// <returns>券番号有無</returns>
        private bool JPDataEditForChangeKind(CHANGEKIND kind, ref long number, bool iserr = false)
        {
            bool ret = false;
            string type = string.Empty;
            long purchase = 0;
            bool exchange = false;
            string amount = Commamodify(purchase);
            switch (kind)
            {
                case CHANGEKIND.NameChange:
                    type = "記名交換";
                    break;
                case CHANGEKIND.MoneyChange:
                    type = "物販交換";
                    break;
                case CHANGEKIND.Change:
                    type = "世代交換";
                    break;
                case CHANGEKIND.ICAttributeChange:
                    type = "属性変更";
                    break;
                case CHANGEKIND.Exchange:
                    exchange = true;
                    break;
            }

            if (exchange)
            {
                // 発行替え
                // 支払方法
                JPDataEditForPayment();
                // 券番号、購入金額
                ret = JPDataEditForPassLine3(ref number, iserr);
                // 種別、チャージ金額、デポジット金額
                JPDataEditForExchange();
            }
            else
            {
                // 発行替え以外
                PrintData.Add("　種別　　" + type + "　　購入金額　　 " + amount);
                PrintData.Add("　媒体　　Ｓｕｉｃａ");
                PrintData.Add("　券種　　ＩＣＳＦ");
                if (session.PersonalInfo.Value.AgeGroup == ReadCardInfo.AdultChildType.NormalChild)
                {
                    PrintData.Add("　大小　　こども");
                }
                else
                {
                    PrintData.Add("　大小　　大人");
                }
            }
            PrintData.Add(string.Empty);
            return ret;
        }

        /// <summary>
        /// ジャーナル印字用の発行替えのデータを編集
        /// </summary>
        private void JPDataEditForExchange()
        {
            string purchase = string.Empty;
            string type = "　種別　　発行替え　　　";
            string printdata = string.Empty;
            long amount = 0;

            if (session.ChargeAmount.Value > 0)
            {
                // 種別、チャージ金額
                purchase = Commamodify(session.ChargeAmount.Value);
                printdata = type + "チャージ　 " + purchase;
                PrintData.Add(printdata);
                // 媒体、デポジット金額
                if (session.PassInfo.Value.Exchange == ExchangeType.Pass)
                {
                    // 磁気→ＩＣ
                    type = "　媒体　　Ｓｕｉｃａ　　";
                    purchase = Commamodify(session.DepositAmount.Value);
                    printdata = type + "デポジット " + purchase;
                    PrintData.Add(printdata);
                }
                else if (session.PassInfo.Value.Exchange == ExchangeType.AllAlone)
                {
                    // 多機能ＩＣ→多機能ＩＣ
                    type = "　媒体　　多機能　　　　";
                    purchase = Commamodify(session.DepositAmount.Value);
                    printdata = type + "デポジット " + purchase;
                    PrintData.Add(printdata);
                }
                else if (session.PassInfo.Value.Exchange == ExchangeType.Suica)
                {
                    // ＩＣ→多機能ＩＣ
                    type = "　媒体　　多機能　　　　";
                    amount = session.ReadCardList.Values[0].Deposit;
                    amount = amount * (-1);
                    purchase = Commamodify(amount);
                    printdata = type + "デポジット " + purchase;
                    PrintData.Add(printdata);
                }
                // 券種、投入金額
                switch (session.PassInfo.Value.Attend)
                {
                    case AttendType.Work:
                        type = "　券種　　通勤　　　　";
                        break;
                    case AttendType.University:
                    case AttendType.Training:
                    case AttendType.HighSchool:
                    case AttendType.JuniorHigh:
                    case AttendType.Elementary:
                    case AttendType.Student:
                        type = "　券種　　通学　　　　";
                        break;
                    case AttendType.Green:
                        type = "　券種　　グリーン　　";
                        break;
                    case AttendType.Frex:
                        type = "　券種　　ＦＲＥＸ　　";
                        break;
                    case AttendType.FrexPal:
                        type = "　券種　　FREXパル　　";
                        break;
                }
                purchase = Commamodify(session.ActualPooledTotal.Value);
                printdata = type + "投入金額　　 " + purchase;
                PrintData.Add(printdata);
                // 大小、おつり
                if (session.PersonalInfo.Value.AgeGroup == ReadCardInfo.AdultChildType.NormalChild)
                {
                    type = "　大小　　こども　　　";
                }
                else
                {
                    type = "　大小　　大人　　　　";
                }
                purchase = Commamodify(session.LogicalChangeBills.Value.Total + session.LogicalChangeCoins.Value.Total);
                printdata = type + "おつり　　　 " + purchase;
                PrintData.Add(printdata);
            }
            else
            {
                // 種別、デポジット金額
                if (session.PassInfo.Value.Exchange == ExchangeType.Suica)
                {
                    purchase = Commamodify(session.ReadCardList.Values[0].Deposit * (-1));
                    printdata = type + "デポジット " + purchase;
                    PrintData.Add(printdata);
                }
                else
                {
                    purchase = Commamodify(session.DepositAmount.Value);
                    printdata = type + "デポジット " + purchase;
                    PrintData.Add(printdata);
                }
                // 媒体、おつり
                if (session.PassInfo.Value.Exchange == ExchangeType.Pass)
                {
                    // 磁気→ＩＣ
                    type = "　媒体　　Ｓｕｉｃａ　";
                }
                else
                {
                    // ＩＣ→多機能ＩＣ
                    // 多機能ＩＣ→多機能ＩＣ
                    type = "　媒体　　多機能　　　";
                }
                purchase = Commamodify(session.LogicalChangeBills.Value.Total + session.LogicalChangeCoins.Value.Total);
                printdata = type + "おつり　　　 " + purchase;
                PrintData.Add(printdata);
                // 券種
                switch (session.PassInfo.Value.Attend)
                {
                    case AttendType.Work:
                        printdata = "　券種　　通勤　　　　";
                        break;
                    case AttendType.University:
                    case AttendType.Training:
                    case AttendType.HighSchool:
                    case AttendType.JuniorHigh:
                    case AttendType.Elementary:
                    case AttendType.Student:
                        printdata = "　券種　　通学";
                        break;
                    case AttendType.Green:
                        printdata = "　券種　　グリーン";
                        break;
                    case AttendType.Frex:
                        printdata = "　券種　　ＦＲＥＸ";
                        break;
                    case AttendType.FrexPal:
                        printdata = "　券種　　FREXパル";
                        break;
                }
                PrintData.Add(printdata);
                // 大小
                if (session.PersonalInfo.Value.AgeGroup == ReadCardInfo.AdultChildType.NormalChild)
                {
                    printdata = "　大小　　こども　　　";
                }
                else
                {
                    printdata = "　大小　　大人　　　　";
                }
                PrintData.Add(printdata);
            }
        }

        /// <summary>
        /// ジャーナル印字用の売上要求を編集
        /// </summary>
        /// <param name="request">要求電文</param>
        private void JPDataEditForSaleRequest(object request)
        {
            MessageSaleRequest origin = new MessageSaleRequest();
            origin = (MessageSaleRequest)request;
            // 端末番号
            int terminalno = int.Parse(origin.SystemControlHeader.SystemUnitNumber);
            // システム通番
            int slipno = int.Parse(origin.SystemControlHeader.SystemSerialNumber);
            DateTime now = System.DateTime.Now;

            PrintData.Add(string.Format("－－売上－－　発行駅 {0:D7}　　端末番号　{1:D5}", Convert.ToInt32(settings.HostCode.Value), terminalno));
            PrintData.Add(string.Format("　システム通番{0:D5} 　{1:D4}年{2:D2}月{3:D2}日{4:D2}時{5:D2}分{6:D2}秒", slipno, now.Year, now.Month, now.Day, now.Hour, now.Minute, now.Second));
        }

        /// <summary>
        /// ジャーナル印字用の売上結果を編集
        /// </summary>
        /// <param name="request">要求電文</param>
        /// <param name="response">結果電文</param>
        /// <param name="isOK">要求結果OK</param>
        private void JPDataEditForSaleResult(object request, object response, bool isOK)
        {
            // 伝票番号
            long slipno = DataStore.GetInstance().TicketNumber.GetTicketNumber(3);
            // 合計金額
            int noall = 0;
            // 商品1～商品3の金額
            int no1 = 0;
            int no2 = 0;
            int no3 = 0;
            // 券番
            string num = "券番　　　";
            // 応答コード
            string code = string.Empty;
            // 商品1～商品3の商品番号
            string code1 = string.Empty;
            string code2 = string.Empty;
            string code3 = string.Empty;
            // 商品1～商品3のＲ通番
            string rcode1 = string.Empty;
            string rcode2 = string.Empty;
            string rcode3 = string.Empty;
            // 要求処理結果フラグ
            string isflag = string.Empty;
            // クレジット合計金額
            string amountall = string.Empty;
            // COMMA追加処理後の商品金額
            string amount1 = string.Empty;
            string amount2 = string.Empty;
            string amount3 = string.Empty;
            // 印字内容
            string printdata = string.Empty;
            DateTime now = System.DateTime.Now;
            MessageSaleRequest origin = new MessageSaleRequest();
            origin = (MessageSaleRequest)request;

            if (response == null)
            {
                // 売上要求結果電文が未受信
                PrintData.Add("＜＜＜＜＜＜＜＜　売上結果未受信　＞＞＞＞＞＞＞");
                PrintData.Add("　　　　　　　金額　　　商品番号　Ｒ通番");

                // 商品1の商品金額、商品番号を取得
                try
                {
                    no1 = int.Parse(origin.Product.Product1Amount);
                }
                catch
                {
                    // セットしない場合に、スペースをセットするので、０を固定
                    no1 = 0;
                }
                if (no1 == 0)
                {
                    // 金額０円の時は”－－－”が印字される
                    code1 = "---";
                }
                else
                {
                    code1 = origin.Product.Product1Code;
                }
                // 商品2の商品金額、商品番号を取得
                try
                {
                    no2 = int.Parse(origin.Product.Product2Amount);
                }
                catch
                {
                    // セットしない場合に、スペースをセットするので、０を固定
                    no2 = 0;
                }
                if (no2 == 0)
                {
                    // 金額０円の時は”－－－”が印字される
                    code2 = "---";
                }
                else
                {
                    code2 = origin.Product.Product2Code;
                }
                // 商品3の商品金額、商品番号を取得
                try
                {
                    no3 = int.Parse(origin.Product.Product3Amount);
                }
                catch
                {
                    // セットしない場合に、スペースをセットするので、０を固定
                    no3 = 0;
                }
                if (no3 == 0)
                {
                    // 金額０円の時は”－－－”が印字される
                    code3 = "---";
                }
                else
                {
                    code3 = origin.Product.Product3Code;
                }

                // クレジット売上の合計金額(商品1金額+商品2金額+商品3金額)、磁気定期券の商品番号
                noall = no1 + no2 + no3;
                amountall = Commamodify2(noall, 6);
                if (session.PassInfo.Value.IssueMedia == IssueMediaType.Pass)
                {
                    // 合計金額と磁気定期券の商品番号
                    printdata = string.Format("　　売上　　　{0}　{1:D3}", amountall, code1);
                }
                else
                {
                    // 合計金額
                    printdata = string.Format("　　売上　　　{0}", amountall);
                }
                PrintData.Add(printdata);

                if ((session.CreditInfo.Value.SaleInfo[0].SaleKind == SaleInfoType.SaleKindType.SEASON) &&
                    (session.PassInfo.Value.IssueMedia != IssueMediaType.Pass))
                {
                    // ＩＣ定期券金額と商品番号
                    amount1 = Commamodify2(no1, 6);
                    printdata = string.Format("　　定期　　　{0}　{1:D3}", amount1, code1);
                    PrintData.Add(printdata);
                }

                if (session.CreditInfo.Value.SaleInfo[0].SaleKind == SaleInfoType.SaleKindType.CHARGE)
                {
                    // チャージ金額と商品番号
                    amount2 = Commamodify2(no1, 5);
                    printdata = string.Format("　　チャージ　 {0}　{1:D3}", amount2, code1);
                    PrintData.Add(printdata);
                }
                else if (session.CreditInfo.Value.SaleInfo[1].SaleKind == SaleInfoType.SaleKindType.CHARGE)
                {
                    // チャージ金額と商品番号
                    amount2 = Commamodify2(no2, 5);
                    printdata = string.Format("　　チャージ　 {0}　{1:D3}", amount2, code2);
                    PrintData.Add(printdata);
                }

                if (session.CreditInfo.Value.SaleInfo[0].SaleKind == SaleInfoType.SaleKindType.DEPOSIT)
                {
                    // デポジット金額と商品番号
                    amount3 = Commamodify2(no1, 5);
                    printdata = string.Format("　　デポジット {0}　{1:D3}", amount3, code1);
                    PrintData.Add(printdata);
                }
                else if (session.CreditInfo.Value.SaleInfo[1].SaleKind == SaleInfoType.SaleKindType.DEPOSIT)
                {
                    // デポジット金額と商品番号
                    amount3 = Commamodify2(no2, 5);
                    printdata = string.Format("　　デポジット {0}　{1:D3}", amount3, code2);
                    PrintData.Add(printdata);
                }
                else if (session.CreditInfo.Value.SaleInfo[2].SaleKind == SaleInfoType.SaleKindType.DEPOSIT)
                {
                    // デポジット金額と商品番号
                    amount3 = Commamodify2(no3, 5);
                    printdata = string.Format("　　デポジット {0}　{1:D3}", amount3, code3);
                    PrintData.Add(printdata);
                }
            }
            else
            {
                // カードシステム処理結果コード
                MessageSaleResponse temp = new MessageSaleResponse();
                temp = (MessageSaleResponse)response;
                isflag = temp.Answer.RequestResultFlag;
                if (isOK)
                {
                    // 受信ＯＫ
                    code = string.Format("{0:D6}-{1:D1}-{2:D4}-{3:D4}", temp.ViewCardControlHeader.ResultCode, isflag, temp.Answer.ErrorCode, temp.Answer.MessageCode);
                    PrintData.Add("－－売上結果－－　応答コード　" + code);
                    PrintData.Add("　会員番号　" + temp.CardNumber.CardNumber);
                    PrintData.Add("　　　　　　　金額　　　商品番号　Ｒ通番");

                    // 商品1の商品金額、商品番号を取得
                    try
                    {
                        no1 = int.Parse(temp.Product.Product1Amount);
                    }
                    catch
                    {
                        // セットしない場合に、スペースをセットするので、０を固定
                        no1 = 0;
                    }
                    if (no1 == 0)
                    {
                        // 金額０円の時は”－－－”が印字される
                        code1 = "---";
                    }
                    else
                    {
                        code1 = temp.Product.Product1Code;
                    }
                    if (temp.Answer.Product1RSerialNumber.Replace(" ", string.Empty).Length == 0)
                    {
                        // Ｒ通番が通知されないとき
                        rcode1 = "----";
                    }
                    else
                    {
                        rcode1 = temp.Answer.Product1RSerialNumber;
                    }
                    // 商品2の商品金額、商品番号を取得
                    try
                    {
                        no2 = int.Parse(temp.Product.Product2Amount);
                    }
                    catch
                    {
                        // セットしない場合に、スペースをセットするので、０を固定
                        no2 = 0;
                    }
                    if (no2 == 0)
                    {
                        // 金額０円の時は”－－－”が印字される
                        code2 = "---";
                    }
                    else
                    {
                        code2 = temp.Product.Product2Code;
                    }
                    if (temp.Answer.Product2RSerialNumber.Replace(" ", string.Empty).Length == 0)
                    {
                        // Ｒ通番が通知されないとき
                        rcode2 = "----";
                    }
                    else
                    {
                        rcode2 = temp.Answer.Product2RSerialNumber;
                    }
                    // 商品3の商品金額、商品番号を取得
                    try
                    {
                        no3 = int.Parse(temp.Product.Product3Amount);
                    }
                    catch
                    {
                        // セットしない場合に、スペースをセットするので、０を固定
                        no3 = 0;
                    }
                    if (no3 == 0)
                    {
                        // 金額０円の時は”－－－”が印字される
                        code3 = "---";
                    }
                    else
                    {
                        code3 = temp.Product.Product3Code;
                    }
                    if (temp.Answer.Product3RSerialNumber.Replace(" ", string.Empty).Length == 0)
                    {
                        // Ｒ通番が通知されないとき
                        rcode3 = "----";
                    }
                    else
                    {
                        rcode3 = temp.Answer.Product3RSerialNumber;
                    }

                    // クレジット売上の合計金額、磁気定期券の商品番号
                    try
                    {
                        noall = int.Parse(temp.Answer.Amount);
                    }
                    catch
                    {
                        noall = 0;
                    }
                    amountall = Commamodify2(noall, 6);
                    if (session.PassInfo.Value.IssueMedia == IssueMediaType.Pass)
                    {
                        // 合計金額と磁気定期券の商品番号
                        printdata = string.Format("　　売上　　　{0}　{1:D3} 　　　{2:4}", amountall, code1, rcode1);
                    }
                    else
                    {
                        // 合計金額
                        printdata = string.Format("　　売上　　　{0}", amountall);
                    }
                    PrintData.Add(printdata);

                    if ((session.CreditInfo.Value.SaleInfo[0].SaleKind == SaleInfoType.SaleKindType.SEASON) &&
                        (session.PassInfo.Value.IssueMedia != IssueMediaType.Pass))
                    {
                        // ＩＣ定期券金額と商品番号
                        amount1 = Commamodify2(no1, 6);
                        printdata = string.Format("　　定期　　　{0}　{1:D3} 　　　{2:4}", amount1, code1, rcode1);
                        PrintData.Add(printdata);
                    }

                    if (session.CreditInfo.Value.SaleInfo[0].SaleKind == SaleInfoType.SaleKindType.CHARGE)
                    {
                        // チャージ金額と商品番号
                        amount2 = Commamodify2(no1, 5);
                        printdata = string.Format("　　チャージ　 {0}　{1:D3} 　　　{2:4}", amount2, code1, rcode1);
                        PrintData.Add(printdata);
                    }
                    else if (session.CreditInfo.Value.SaleInfo[1].SaleKind == SaleInfoType.SaleKindType.CHARGE)
                    {
                        // チャージ金額と商品番号
                        amount2 = Commamodify2(no2, 5);
                        printdata = string.Format("　　チャージ　 {0}　{1:D3} 　　　{2:4}", amount2, code2, rcode2);
                        PrintData.Add(printdata);
                    }

                    if (session.CreditInfo.Value.SaleInfo[0].SaleKind == SaleInfoType.SaleKindType.DEPOSIT)
                    {
                        // デポジット金額と商品番号
                        amount3 = Commamodify2(no1, 5);
                        printdata = string.Format("　　デポジット {0}　{1:D3} 　　　{2:4}", amount3, code1, rcode1);
                        PrintData.Add(printdata);
                    }
                    else if (session.CreditInfo.Value.SaleInfo[1].SaleKind == SaleInfoType.SaleKindType.DEPOSIT)
                    {
                        // デポジット金額と商品番号
                        amount3 = Commamodify2(no2, 5);
                        printdata = string.Format("　　デポジット {0}　{1:D3} 　　　{2:4}", amount3, code2, rcode2);
                        PrintData.Add(printdata);
                    }
                    else if (session.CreditInfo.Value.SaleInfo[2].SaleKind == SaleInfoType.SaleKindType.DEPOSIT)
                    {
                        // デポジット金額と商品番号
                        amount3 = Commamodify2(no3, 5);
                        printdata = string.Format("　　デポジット {0}　{1:D3} 　　　{2:4}", amount3, code3, rcode3);
                        PrintData.Add(printdata);
                    }

                    if ((session.Operation.Value == Session.OperationType.Pass) &&
                        ((session.PassInfo.Value.ChoiceProcess == ChoiceProcessType.New) ||
                         (session.PassInfo.Value.ChoiceProcess == ChoiceProcessType.Continue) ||
                         (session.PassInfo.Value.ChoiceProcess == ChoiceProcessType.TakeOver)))
                    {
                        // 定期券業務
                        bool newMedia = false;
                        bool updateMedia = true;
                        // 発行媒体取得
                        SessionSaveTable SessionData = new SessionSaveTable();
                        session.GetSessionDataStore(SessionData);
                        SessionData.GetICMedia(ref newMedia, ref updateMedia);
                        if (newMedia)
                        {
                            // 新規媒体
                            num = num + session.IssueIcCardInfo[0].PassSerialNumber.ToString("00000");
                        }
                        else if (updateMedia)
                        {
                            // 既存媒体
                            num = num + session.ReadCardList[0].PassSerialNumber.ToString("00000");
                        }
                        else
                        {
                            // 既存媒体
                            num = num + session.ReadCardList[1].PassSerialNumber.ToString("00000");
                        }
                    }
                    else
                    {
                        // ＩＣＳＦ購入時、・チャージ時は、券番印字されない
                        num = string.Empty;
                    }
                    PrintData.Add(string.Format("　伝票番号　{0:D5} 　　　　　　　", slipno) + num);
                }
                else
                {
                    // 受信結果ＮＧ
                    code = string.Format("{0:D6}-{1:D1}-{2:D4}-{3:D4}", temp.ViewCardControlHeader.ResultCode, isflag, temp.Answer.ErrorCode, temp.Answer.MessageCode);
                    PrintData.Add("－－売上結果－－　応答コード　" + code);
                    PrintData.Add("＜＜＜＜＜＜＜＜　売上結果異常　＞＞＞＞＞＞＞＞");
                }
                // 売上要求結果電文が受信
                if (!ResultDataCheck(request, response))
                {
                    // 受信電文チェックＮＧ、かつ結果電文受信済みの場合
                    // 都度カウンタ異常印字を実施
                    JPPrintCreateDataForErrorPrint(false, request, response);
                }
            }
        }

        /// <summary>
        /// ジャーナル印字用の内部控除要求を編集
        /// </summary>
        /// <param name="request">要求電文</param>
        private void JPDataEditForDeductRequest(object request)
        {
            // タイトル
            PrintData.Add("－内部控除－");
            // 異常内容
            Errors errors = DataStore.GetInstance().Errors;
            // 異常情報を表示
            string errorWord = string.Empty;
            foreach (var value in errors.ErrorList.Values)
            {
                errorWord = ErrorCodeConverter.Convert(value.EC).Error;
                PrintData.Add("異常内容　" + errorWord);
            }
        }

        /// <summary>
        /// ジャーナル印字用の内部控除結果を編集
        /// </summary>
        /// <param name="request">要求電文</param>
        /// <param name="response">結果電文</param>
        /// <param name="isOK">要求結果OK</param>
        private void JPDataEditForDeductResult(object request, object response, bool isOK)
        {
            string code = string.Empty;

            if (response == null)
            {
                // 内部控除結果電文未受信時
                PrintData.Add("＜＜＜＜＜＜＜＜　内部控除結果未受信　＞＞＞＞＞");
            }
            else
            {
                // 内部控除結果電文が受信
                MessageDeductionResponse result = new MessageDeductionResponse();
                result = (MessageDeductionResponse)response;
                // カードシステム処理結果コード
                PrintData.Add(string.Format("－内部控除結果－　応答コード　{0:D6}", result.ViewCardControlHeader.ResultCode));
                if (!isOK)
                {
                    // 内部控除結果電文ＮＧ受信時
                    PrintData.Add("＜＜＜＜＜＜＜＜　内部控除結果異常　＞＞＞＞＞＞");
                }
                if (!ResultDataCheck(request, response))
                {
                    // 受信電文チェックＮＧ、かつ結果電文受信済みの場合
                    // 都度カウンタ異常印字を実施
                    JPPrintCreateDataForErrorPrint(false, request, response);
                }
            }
        }

        /// <summary>
        /// ジャーナル印字用の売上戻し要求を編集
        /// </summary>
        /// <param name="request">要求電文</param>
        private void JPDataEditForRefundRequest(object request)
        {
            MessageSaleReturnRequest origin = new MessageSaleReturnRequest();
            origin = (MessageSaleReturnRequest)request;

            PrintData.Add("－売上戻し－");
            PrintData.Add(string.Format("　システム通番{0:D5}", origin.SystemControlHeader.SystemSerialNumber));

            // 異常内容
            Errors errors = DataStore.GetInstance().Errors;
            // 異常情報を表示
            string errorWord = string.Empty;
            foreach (var value in errors.ErrorList.Values)
            {
                errorWord = ErrorCodeConverter.Convert(value.EC).Error;
                PrintData.Add("異常内容　" + errorWord);
            }
        }

        /// <summary>
        /// ジャーナル印字用の売上戻し結果を編集
        /// </summary>
        /// <param name="request">要求電文</param>
        /// <param name="response">結果電文</param>
        /// <param name="isOK">要求結果OK</param>
        private void JPDataEditFoRefundResult(object request, object response, bool isOK)
        {
            string code = string.Empty;
            string amount = string.Empty;
            string printdata = string.Empty;

            if (response == null)
            {
                // 売上戻し要求結果電文が未受信
                PrintData.Add("＜＜＜＜＜＜＜＜　売上戻し結果未受信　＞＞＞＞＞");
            }
            else
            {
                // 受信電文チェックＯＫ
                MessageSaleReturnResponse result = new MessageSaleReturnResponse();
                result = (MessageSaleReturnResponse)response;
                PrintData.Add(string.Format("－売上戻し結果－　応答コード　{0:D6}", result.ViewCardControlHeader.ResultCode));
                if (!isOK)
                {
                    // 売上戻し受信結果ＮＧ
                    PrintData.Add("＜＜＜＜＜＜＜＜　売上戻し結果異常　＞＞＞＞＞＞");
                }
                // 売上戻し結果電文が受信
                if (!ResultDataCheck(request, response))
                {
                    // 受信電文チェックＮＧ、かつ結果電文受信済みの場合
                    // 都度カウンタ異常印字を実施
                    JPPrintCreateDataForErrorPrint(false, request, response);
                }
            }
        }

        /// <summary>
        /// ジャーナル印字用のカウンタ要求を編集
        /// </summary>
        /// <param name="request">要求電文</param>
        private void JPDataEditForCountRequest(object request)
        {
            MessageCountRequest origin = new MessageCountRequest();
            origin = (MessageCountRequest)request;
            // 端末番号
            int terminalno = int.Parse(origin.SystemControlHeader.SystemUnitNumber);
            // システム通番
            int slipno = int.Parse(origin.SystemControlHeader.SystemSerialNumber);
            // 合計金額
            int allamount = 0;
            // 売上戻し金額
            int returnamount = 0;
            // 内部控除金額
            int deductionamount = 0;
            // 合計件数
            string allnum = string.Empty;
            // 売上戻し件数
            string retrunnum = string.Empty;
            // 内部控除件数
            string deductionnum = string.Empty;
            string amount1 = string.Empty;
            string amount2 = string.Empty;
            string amount3 = string.Empty;
            string code = string.Empty;
            string printdata = string.Empty;
            DateTime now = System.DateTime.Now;

            PrintData.Add(string.Format("－－締切－－　発行駅 {0:D7}　　端末番号　{1:D5}", Convert.ToInt32(settings.HostCode.Value), terminalno));
            PrintData.Add(string.Format("　システム通番{0:D5} 　{1:D4}年{2:D2}月{3:D2}日{4:D2}時{5:D2}分{6:D2}秒", slipno, now.Year, now.Month, now.Day, now.Hour, now.Minute, now.Second));
            // クレジット購入の合計件数と合計金額
            allnum = origin.Counter.SaleCounterNumber.PadLeft(5);
            allamount = int.Parse(origin.Counter.SaleCounterAmount);
            amount1 = Commamodify2(allamount, 9);
            printdata = "　合計件数　　　 " + allnum + "件　　金額　" + amount1;
            PrintData.Add(printdata);
            // 売上戻し件数と売上戻し金額
            retrunnum = origin.Counter.SaleReturnCounterNumber.PadLeft(5);
            returnamount = int.Parse(origin.Counter.SaleReturnCounterAmount);
            amount2 = Commamodify2(returnamount, 9);
            printdata = "　売上戻し件数　 " + retrunnum + "件　　金額　" + amount2;
            PrintData.Add(printdata);
            // 内部控除件数と内部控除金額
            deductionnum = origin.Counter.DeductCounterNumber.PadLeft(5);
            deductionamount = int.Parse(origin.Counter.DeductCounterAmount);
            amount3 = Commamodify2(deductionamount, 9);
            printdata = "　内部控除件数　 " + deductionnum + "件　　金額　" + amount3;
            PrintData.Add(printdata);
        }

        /// <summary>
        /// ジャーナル印字用のカウンタ結果を編集
        /// </summary>
        /// <param name="request">要求電文</param>
        /// <param name="response">結果電文</param>
        /// <param name="isOK">要求結果OK</param>
        private void JPDataEditForCountResult(object request, object response, bool isOK)
        {
            string code = string.Empty;
            string amount = string.Empty;
            string printdata = string.Empty;

            if (response == null)
            {
                // カウンタ通知電文未受信
                PrintData.Add("＜＜＜＜＜＜＜＜　締切結果未受信　＞＞＞＞＞＞＞");
            }
            else
            {
                // カウンタ通知電文結果受信
                MessageCountResponse result = new MessageCountResponse();
                result = (MessageCountResponse)response;
                PrintData.Add(string.Format("－－締切結果－－　応答コード　{0:D6}", result.ViewCardControlHeader.ResultCode));
                if (!isOK)
                {
                    // カウンタ通知電文受信結果ＮＧ
                    PrintData.Add("＜＜＜＜＜＜＜＜　締切結果異常　＞＞＞＞＞＞＞＞");
                }
                // 空行
                PrintData.Add(string.Empty);

                // 精算処理結果電文が受信
                if (!ResultDataCheck(request, response))
                {
                    // 受信電文チェックＮＧ、かつ結果電文受信済みの場合
                    // 都度カウンタ異常印字を実施
                    JPPrintCreateDataForErrorPrint(true, request, response);
                    PrintData.Add(string.Empty);
                }
            }
        }

        /// <summary>
        /// ジャーナル印字用の開局要求を編集
        /// </summary>
        /// <param name="request">要求電文</param>
        private void JPDataEditForOpenRequest(object request)
        {
            MessageOpenRequest origin = new MessageOpenRequest();
            origin = (MessageOpenRequest)request;
            // 端末番号
            int terminalno = int.Parse(origin.SystemControlHeader.SystemUnitNumber);
            // システム通番
            int slipno = int.Parse(origin.SystemControlHeader.SystemSerialNumber);
            DateTime now = System.DateTime.Now;

            // 空行
            PrintData.Add(string.Empty);
            PrintData.Add(string.Format("－－開局－－　発行駅 {0:D7}　　端末番号　{1:D5}", Convert.ToInt32(settings.HostCode.Value), terminalno));
            PrintData.Add(string.Format("　システム通番{0:D5} 　{1:D4}年{2:D2}月{3:D2}日{4:D2}時{5:D2}分{6:D2}秒", slipno, now.Year, now.Month, now.Day, now.Hour, now.Minute, now.Second));
        }

        /// <summary>
        /// ジャーナル印字用の開局結果を編集
        /// </summary>
        /// <param name="request">要求電文</param>
        /// <param name="response">要求結果電文</param>
        /// <param name="isOK">要求結果OK</param>
        private void JPDataEditForOpenResult(object request, object response, bool isOK)
        {
            string code = string.Empty;

            if (response == null)
            {
                // 開局要求結果電文が未受信
                PrintData.Add("＜＜＜＜＜＜＜＜　開局結果未受信　＞＞＞＞＞＞＞");
            }
            else
            {
                // 受信電文チェックＯＫ
                // カードシステム処理結果コード
                MessageOpenResponse result = new MessageOpenResponse();
                result = (MessageOpenResponse)response;
                PrintData.Add(string.Format("－－開局結果－－　応答コード　{0:D6}", result.ViewCardControlHeader.ResultCode));
                if (!isOK)
                {
                    // 受信結果ＮＧ
                    PrintData.Add("＜＜＜＜＜＜＜＜　開局結果異常　＞＞＞＞＞＞＞＞");
                }
                // 精算処理結果電文が受信
                if (!ResultDataCheck(request, response))
                {
                    // 受信電文チェックＮＧ、かつ結果電文受信済みの場合
                    // 都度カウンタ異常印字を実施
                    JPPrintCreateDataForErrorPrint(false, request, response);
                }
            }
        }

        /// <summary>
        /// ジャーナル印字用の精算処理用異常印字データを編集
        /// </summary>
        /// <param name="request">要求電文</param>
        /// <param name="response">要求結果電文</param>
        private void JPDataEditForErrorAdjust(object request, object response)
        {
            MessageCountRequest origin = new MessageCountRequest();
            origin = (MessageCountRequest)request;
            MessageCountResponse result = new MessageCountResponse();
            result = (MessageCountResponse)response;
            // データ変換用
            string amount1 = string.Empty;
            string amount2 = string.Empty;
            string amount3 = string.Empty;
            string no1 = string.Empty;
            string no2 = string.Empty;
            string no3 = string.Empty;
            string printdata = string.Empty;
            // 多機能券売機用件数
            string mnum1 = origin.Counter.SaleCounterNumber;
            string mnum2 = origin.Counter.SaleReturnCounterNumber;
            string mnum3 = origin.Counter.DeductCounterNumber;
            // ビューホスト用件数
            string vnum1 = result.Counter.SaleCounterNumber;
            string vnum2 = result.Counter.SaleReturnCounterNumber;
            string vnum3 = result.Counter.DeductCounterNumber;
            // 多機能券売機用金額
            int mamount1 = int.Parse(origin.Counter.SaleCounterAmount);
            int mamount2 = int.Parse(origin.Counter.SaleReturnCounterAmount);
            int mamount3 = int.Parse(origin.Counter.DeductCounterAmount);
            // ビューホスト用金額
            int vamount1 = int.Parse(result.Counter.SaleCounterAmount);
            int vamount2 = int.Parse(result.Counter.SaleReturnCounterAmount);
            int vamount3 = int.Parse(result.Counter.DeductCounterAmount);

            // タイトル
            PrintData.Add("　　　　　多機能券売機　　　　ビューホスト");
            // 多機能カウンタ金額、ビューホストカウンタ金額
            amount1 = Commamodify2(mamount1, 9);
            no1 = Commamodify2(vamount1, 9);
            // 多機能売上戻し金額、ビューホスト売上戻し金額
            amount2 = Commamodify2(mamount2, 9);
            no2 = Commamodify2(vamount2, 9);
            // 多機能内部控除金額、ビューホスト内部控除金額
            amount3 = Commamodify2(mamount3, 9);
            no3 = Commamodify2(vamount3, 9);

            printdata = string.Format("売上　　　{0:D5} {1}　{2:D5} {3}", mnum1, amount1, vnum1, no1);
            PrintData.Add(printdata);
            printdata = string.Format("売上戻し　{0:D5} {1}　{2:D5} {3}", mnum2, amount2, vnum2, no1);
            PrintData.Add(printdata);
            printdata = string.Format("内部控除　{0:D5} {1}　{2:D5} {3}", mnum3, amount3, vnum3, no1);
            PrintData.Add(printdata);
        }

        /// <summary>
        /// ジャーナル印字用の異常印字データを編集
        /// </summary>
        /// <param name="request">要求電文</param>
        /// <param name="response">要求結果電文</param>
        private void JPDataEditForErrorPrint(object request, object response)
        {
            string code = string.Empty;
            string printdata = string.Empty;
            // データ変換用
            string amount1 = string.Empty;
            string amount2 = string.Empty;
            string no1 = string.Empty;
            string no2 = string.Empty;
            // 多機能券売機用件数
            string mnum1 = string.Empty;
            string mnum2 = string.Empty;
            // ビューホスト用件数
            string vnum1 = string.Empty;
            string vnum2 = string.Empty;
            // 多機能券売機用金額
            int mamount1 = 0;
            int mamount2 = 0;
            // ビューホスト用金額
            int vamount1 = 0;
            int vamount2 = 0;

            if (request.GetType() == typeof(MessageOpenRequest))
            {
                // 開局処理
                MessageOpenRequest origin = new MessageOpenRequest();
                MessageOpenResponse result = new MessageOpenResponse();
                origin = (MessageOpenRequest)request;
                result = (MessageOpenResponse)response;
                mnum1 = origin.SystemControlHeader.SaleNumber;
                mnum2 = origin.SystemControlHeader.SaleReturnNumber;
                vnum1 = result.SystemControlHeader.SaleNumber;
                vnum2 = result.SystemControlHeader.SaleReturnNumber;
                mamount1 = int.Parse(origin.SystemControlHeader.SaleAmount);
                mamount2 = int.Parse(origin.SystemControlHeader.SaleReturnAmount);
                vamount1 = int.Parse(result.SystemControlHeader.SaleAmount);
                vamount2 = int.Parse(result.SystemControlHeader.SaleReturnAmount);
            }
            else if (request.GetType() == typeof(MessageSaleRequest))
            {
                // 売上要求
                MessageSaleRequest origin = new MessageSaleRequest();
                MessageSaleResponse result = new MessageSaleResponse();
                origin = (MessageSaleRequest)request;
                result = (MessageSaleResponse)response;
                mnum1 = origin.SystemControlHeader.SaleNumber;
                mnum2 = origin.SystemControlHeader.SaleReturnNumber;
                vnum1 = result.SystemControlHeader.SaleNumber;
                vnum2 = result.SystemControlHeader.SaleReturnNumber;
                mamount1 = int.Parse(origin.SystemControlHeader.SaleAmount);
                mamount2 = int.Parse(origin.SystemControlHeader.SaleReturnAmount);
                vamount1 = int.Parse(result.SystemControlHeader.SaleAmount);
                vamount2 = int.Parse(result.SystemControlHeader.SaleReturnAmount);
            }
            else if (request.GetType() == typeof(MessageSaleReturnRequest))
            {
                // 売上戻し要求
                MessageSaleReturnRequest origin = new MessageSaleReturnRequest();
                MessageSaleReturnResponse result = new MessageSaleReturnResponse();
                origin = (MessageSaleReturnRequest)request;
                result = (MessageSaleReturnResponse)response;
                mnum1 = origin.SystemControlHeader.SaleNumber;
                mnum2 = origin.SystemControlHeader.SaleReturnNumber;
                vnum1 = result.SystemControlHeader.SaleNumber;
                vnum2 = result.SystemControlHeader.SaleReturnNumber;
                mamount1 = int.Parse(origin.SystemControlHeader.SaleAmount);
                mamount2 = int.Parse(origin.SystemControlHeader.SaleReturnAmount);
                vamount1 = int.Parse(result.SystemControlHeader.SaleAmount);
                vamount2 = int.Parse(result.SystemControlHeader.SaleReturnAmount);
            }
            else if (request.GetType() == typeof(MessageDeductionRequest))
            {
                // 内部控除要求
                MessageDeductionRequest origin = new MessageDeductionRequest();
                MessageDeductionResponse result = new MessageDeductionResponse();
                origin = (MessageDeductionRequest)request;
                result = (MessageDeductionResponse)response;
                mnum1 = origin.SystemControlHeader.SaleNumber;
                mnum2 = origin.SystemControlHeader.SaleReturnNumber;
                vnum1 = result.SystemControlHeader.SaleNumber;
                vnum2 = result.SystemControlHeader.SaleReturnNumber;
                mamount1 = int.Parse(origin.SystemControlHeader.SaleAmount);
                mamount2 = int.Parse(origin.SystemControlHeader.SaleReturnAmount);
                vamount1 = int.Parse(result.SystemControlHeader.SaleAmount);
                vamount2 = int.Parse(result.SystemControlHeader.SaleReturnAmount);
            }

            // タイトル
            PrintData.Add("　　　　　多機能券売機　　　　ビューホスト");

            // 多機能カウンタ金額、ビューホストカウンタ金額
            amount1 = Commamodify2(mamount1, 9);
            no1 = Commamodify2(vamount1, 9);
            // 多機能売上戻し金額、ビューホスト売上戻し金額
            amount2 = Commamodify2(mamount2, 9);
            no2 = Commamodify2(vamount2, 9);

            printdata = string.Format("売上　　　{0:5} {1}　{2:5} {3}", mnum1, amount1, vnum1, no1);
            PrintData.Add(printdata);
            printdata = string.Format("売上戻し　{0:5} {1}　{2:5} {3}", mnum2, amount2, vnum2, no2);
            PrintData.Add(printdata);
        }

        /// <summary>
        /// COMMA追加処理
        /// </summary>
        /// <param name="amount">変更要金額</param>
        /// <returns>COMMA追加後文字列</returns>
        private string Commamodify(long amount)
        {
            if (amount == 0)
            {
                return "￥ 0".PadLeft(9);
            }
            else
            {
                return string.Format("￥ {0:#,###}", amount).PadLeft(9);
            }
        }

        /// <summary>
        /// 与信受信の券番号取得処理
        /// </summary>
        /// <param name="num">券番号</param>
        /// <returns>券番号有無</returns>
        private bool NumberJudge(ref long num)
        {
            bool ret = true;
            if ((session.Operation.Value == Session.OperationType.Pass) &&
                ((session.PassInfo.Value.ChoiceProcess == ChoiceProcessType.New) ||
                 (session.PassInfo.Value.ChoiceProcess == ChoiceProcessType.Continue) ||
                 (session.PassInfo.Value.ChoiceProcess == ChoiceProcessType.TakeOver)))
            {
                // 定期券業務
                bool newMedia = false;
                bool updateMedia = true;
                // 発行媒体取得
                SessionSaveTable SessionData = new SessionSaveTable();
                session.GetSessionDataStore(SessionData);
                SessionData.GetICMedia(ref newMedia, ref updateMedia);
                if (newMedia)
                {
                    // 新規媒体
                    num = session.IssueIcCardInfo.Values[0].PassSerialNumber;
                }
                else if (updateMedia)
                {
                    // 既存媒体
                    num = session.ReadCardList[0].PassSerialNumber;
                }
                else
                {
                    // 既存媒体
                    num = session.ReadCardList[1].PassSerialNumber;
                }
            }
            else
            {
                // ＩＣＳＦ購入時、・チャージ時は、券番印字されない
                ret = false;
            }

            return ret;
        }

        /// <summary>
        /// COMMA追加処理（VIEWホスト送信用）
        /// </summary>
        /// <param name="amount">変更要金額</param>
        /// <param name="num">入力桁数</param>
        /// <returns>COMMA追加後文字列</returns>
        private string Commamodify2(long amount, int num)
        {
            string ret = string.Empty;
            if (amount == 0)
            {
                switch (num)
                {
                    case 5:
                        ret = " \\" + "0".PadLeft(6);
                        break;
                    case 6:
                        ret = "\\" + "0".PadLeft(7);
                        break;
                    case 9:
                        ret = "\\" + "0".PadLeft(11);
                        break;
                    default:
                        break;
                }
            }
            else
            {
                switch (num)
                {
                    case 5:
                        ret = " \\" + string.Format("{0:###,###,###}", amount).PadLeft(6);
                        break;
                    case 6:
                        ret = "\\" + string.Format("{0:###,###,###}", amount).PadLeft(7);
                        break;
                    case 9:
                        ret = "\\" + string.Format("{0:###,###,###}", amount).PadLeft(11);
                        break;
                    default:
                        break;
                }
            }
            return ret;
        }

        /// <summary>
        /// IDiをマスクがけ状態にする
        /// </summary>
        /// <param name="idi">IDi文字列</param>
        /// <returns>マスクがけしたIDi文字列</returns>
        private string MaskIDi(string idi)
        {
            string ret = idi;
            if (idi.Length > 8)
            {
                ret = idi.Substring(0, 2) + "＊＊＊＊＊＊＊＊＊＊＊" + idi.Substring(idi.Length - 4, 4);
            }
            return ret;
        }
        
        /// <summary>
        /// 受信結果電文のチェック
        /// </summary>
        /// <param name="request">元要求電文</param>
        /// <param name="response">要求結果電文</param>
        /// <returns>チェック結果（True:一致 False:一致しない）</returns>
        private bool ResultDataCheck(object request, object response)
        {
            bool ret = true;
            if (request.GetType() == typeof(MessageOpenRequest))
            {
                // 開局処理要求
                MessageOpenRequest temp = new MessageOpenRequest();
                MessageOpenResponse check = new MessageOpenResponse();
                temp = (MessageOpenRequest)request;
                check = (MessageOpenResponse)response;

                if ((temp.SystemControlHeader.SaleNumber != check.SystemControlHeader.SaleNumber) ||
                    (temp.SystemControlHeader.SaleAmount != check.SystemControlHeader.SaleAmount) ||
                    (temp.SystemControlHeader.SaleReturnNumber != check.SystemControlHeader.SaleReturnNumber) ||
                    (temp.SystemControlHeader.SaleReturnAmount != check.SystemControlHeader.SaleReturnAmount))
                {
                    // 都度カウンタ情報の内容が不一致
                    ret = false;
                }
            }
            else if (request.GetType() == typeof(MessageSaleRequest))
            {
                // 売上要求
                MessageSaleRequest temp = new MessageSaleRequest();
                MessageSaleResponse check = new MessageSaleResponse();
                temp = (MessageSaleRequest)request;
                check = (MessageSaleResponse)response;

                if ((temp.SystemControlHeader.SaleNumber != check.SystemControlHeader.SaleNumber) ||
                    (temp.SystemControlHeader.SaleAmount != check.SystemControlHeader.SaleAmount) ||
                    (temp.SystemControlHeader.SaleReturnNumber != check.SystemControlHeader.SaleReturnNumber) ||
                    (temp.SystemControlHeader.SaleReturnAmount != check.SystemControlHeader.SaleReturnAmount))
                {
                    // 都度カウンタ情報の内容が不一致
                    ret = false;
                }
            }
            else if (request.GetType() == typeof(MessageSaleReturnRequest))
            {
                // 売上戻し要求
                MessageSaleReturnRequest temp = new MessageSaleReturnRequest();
                MessageSaleReturnResponse check = new MessageSaleReturnResponse();
                temp = (MessageSaleReturnRequest)request;
                check = (MessageSaleReturnResponse)response;

                if ((temp.SystemControlHeader.SaleNumber != check.SystemControlHeader.SaleNumber) ||
                    (temp.SystemControlHeader.SaleAmount != check.SystemControlHeader.SaleAmount) ||
                    (temp.SystemControlHeader.SaleReturnNumber != check.SystemControlHeader.SaleReturnNumber) ||
                    (temp.SystemControlHeader.SaleReturnAmount != check.SystemControlHeader.SaleReturnAmount))
                {
                    // 都度カウンタ情報の内容が不一致
                    ret = false;
                }
            }
            else if (request.GetType() == typeof(MessageDeductionRequest))
            {
                // 内部控除要求
                MessageDeductionRequest temp = new MessageDeductionRequest();
                MessageDeductionResponse check = new MessageDeductionResponse();
                temp = (MessageDeductionRequest)request;
                check = (MessageDeductionResponse)response;

                if ((temp.SystemControlHeader.SaleNumber != check.SystemControlHeader.SaleNumber) ||
                    (temp.SystemControlHeader.SaleAmount != check.SystemControlHeader.SaleAmount) ||
                    (temp.SystemControlHeader.SaleReturnNumber != check.SystemControlHeader.SaleReturnNumber) ||
                    (temp.SystemControlHeader.SaleReturnAmount != check.SystemControlHeader.SaleReturnAmount))
                {
                    // 都度カウンタ情報の内容が不一致
                    ret = false;
                }
            }
            else if (request.GetType() == typeof(MessageCountRequest))
            {
                // 精算処理要求
                MessageCountRequest temp = new MessageCountRequest();
                MessageCountResponse check = new MessageCountResponse();
                temp = (MessageCountRequest)request;
                check = (MessageCountResponse)response;
                TotalizeDataSearch TotalizeDayData = new TotalizeDataSearch(TotalizeDataCount.GetInstance().GetData(DataCountBase.Generation.Cur, TotalizeDataCount.TotalizeKind.Day, DataCountLabels.CategorySaleData));
                if ((temp.ViewCardControlHeader.ResultCode == "20JR21") ||
                    (temp.ViewCardControlHeader.ResultCode == "20JR22"))
                {
                    // カウンタ値正常、またはカウンタ値異常の場合に、
                    // カウンタ比較を実施する
                    if ((TotalizeDayData.GetData(DataCountLabels.CreditSaleAmount, 8) != long.Parse(check.Counter.SaleCounterAmount)) ||
                        (TotalizeDayData.GetData(DataCountLabels.CreditSaleNum, 6) != long.Parse(check.Counter.SaleCounterNumber)) ||
                        (TotalizeDayData.GetData(DataCountLabels.CreditRefundAmount, 8) != long.Parse(check.Counter.SaleReturnCounterAmount)) ||
                        (TotalizeDayData.GetData(DataCountLabels.CreditRefundNum, 6) != long.Parse(check.Counter.SaleReturnCounterNumber)) ||
                        (TotalizeDayData.GetData(DataCountLabels.CreditDeductAmount, 8) != long.Parse(check.Counter.DeductCounterAmount)) ||
                        (TotalizeDayData.GetData(DataCountLabels.CreditDeductNum, 6) != long.Parse(check.Counter.DeductCounterNumber)))
                    {
                        // カウンタ情報情報の内容が不一致
                        ret = false;
                    }
                }
            }
            return ret;
        }
        #endregion

        /// <summary>
        /// 現金管理帳票情報作成
        /// </summary>
        /// <returns>帳票データ</returns>
        public byte[] CashManagementPrintDataCreateData()
        {
            byte[] ticketOutput = null;
            if (operatorInfo.CashManagementJob == OperatorInfo.CashManagementJobType.CollectBillFull ||
                operatorInfo.CashManagementJob == OperatorInfo.CashManagementJobType.SplitMoney ||
                operatorInfo.CashManagementJob == OperatorInfo.CashManagementJobType.SupplyAdd ||
                operatorInfo.CashManagementJob == OperatorInfo.CashManagementJobType.SupplyStart ||
                operatorInfo.CashManagementJob == OperatorInfo.CashManagementJobType.ReserveCollect)
            {
                // 発券情報作成
                var TicketMediaInput = new MediaDataInput();
                var JournalInput = new JournalDataInput();
                var journalData = new JournalData();
                journalData.LineData = MakePrintData().ToArray();
                List<JournalData> jourData = new List<JournalData>();
                jourData.Add(journalData);
                JournalInput.Data = jourData.ToArray();
                TicketMediaInput.Info = JournalInput.GetBytes();
                TicketMediaInput.EditKind = MediaDataInput.MediaEditKind.EditJournal;
                ticketOutput = TicketMediaInput.GetBytes();
            }
            return ticketOutput;
        }

        /// <summary>
        /// 現金管理帳票情報作成(RH)
        /// </summary>
        /// <returns>帳票データ</returns>
        public byte[] CashManagementPrintDataCreateDataRH()
        {
            byte[] ticketOutput = null;
            if (operatorInfo.CashManagementJob == OperatorInfo.CashManagementJobType.CollectBillFull ||
                operatorInfo.CashManagementJob == OperatorInfo.CashManagementJobType.SplitMoney ||
                operatorInfo.CashManagementJob == OperatorInfo.CashManagementJobType.SupplyAdd ||
                operatorInfo.CashManagementJob == OperatorInfo.CashManagementJobType.SupplyStart ||
                operatorInfo.CashManagementJob == OperatorInfo.CashManagementJobType.ReserveCollect)
            {
                // 発券情報作成
                var TicketMediaInput = new MediaDataInput();
                var RhInput = new RhFreeFormatInput();
                RhInput.PrintInfo = MakePrintData().ToArray();
                TicketMediaInput.Info = RhInput.GetBytes();
                TicketMediaInput.EditKind = MediaDataInput.MediaEditKind.EditRhFreeFormat;
                ticketOutput = TicketMediaInput.GetBytes();
            }
            return ticketOutput;
        }

        /// <summary>
        /// 処理未了履歴印字データ作成
        /// </summary>
        /// <returns>帳票データ</returns>
        public byte[] IcWriteUnfinishedCreateData()
        {
            byte[] ticketOutput = null;
            // 発券情報作成
            var TicketMediaInput = new MediaDataInput();
            var RhInput = new RhFreeFormatInput();
            RhInput.PrintInfo = MakePrintDataForWriteUnfinishedCollect().ToArray();
            TicketMediaInput.Info = RhInput.GetBytes();
            TicketMediaInput.EditKind = MediaDataInput.MediaEditKind.EditRhFreeFormat;
            ticketOutput = TicketMediaInput.GetBytes();
            return ticketOutput;
        }

        /// <summary>
        /// 印字情報作成処理
        /// </summary>
        /// <returns>印字情報</returns>
        public List<string> MakePrintData()
        {
            var data = new List<string>();

            switch (operatorInfo.CashManagementJob)
            {
                case OperatorInfo.CashManagementJobType.SupplyStart:
                    MakePrintDataForSupplyStart(ref data);
                    break;
                case OperatorInfo.CashManagementJobType.SupplyAdd:
                    MakePrintDataForSupplyAdd(ref data);
                    break;
                case OperatorInfo.CashManagementJobType.SplitMoney:
                    MakePrintDataForSplitMoney(ref data);
                    break;
                case OperatorInfo.CashManagementJobType.CollectBillFull:
                    MakePrintDataForCollectBillFull(ref data);
                    break;
                case OperatorInfo.CashManagementJobType.ReserveCollect:
                    MakePrintDataForReserveCollect(ref data);
                    break;
                default: break;
            }

            return data;
        }

        /// <summary>
        /// 印字情報作成処理（始業補給）
        /// </summary>
        /// <param name="data">印字情報</param>
        private void MakePrintDataForSupplyStart(ref List<string> data)
        {
            MakePrintDataForSupplyAdd(ref data);
            data[0] = "準備金補給データ　　　　　　　　　　";
            data[4] = data[4].Replace("入金", "補給");
            data[8] = data[8].Replace("入金", "補給");
            data[9] = data[9].Replace("入金", "補給");
        }

        /// <summary>
        /// 印字情報作成処理（追加補給）
        /// </summary>
        /// <param name="data">印字情報</param>
        private void MakePrintDataForSupplyAdd(ref List<string> data)
        {
            // 1行目 タイトル
            string line01 = "入金データ　　　　　　　　　　　　　";
            data.Add(line01);

            // 2行目 日時（西暦）
            string line02 = string.Format(
                "{0}年{1}月{2}日　{3}：{4}",
                Strings.StrConv(operatorInfo.TradeTime.Year.ToString(), VbStrConv.Wide),
                Strings.StrConv(operatorInfo.TradeTime.Month.ToString().PadLeft(2, '0'), VbStrConv.Wide),
                Strings.StrConv(operatorInfo.TradeTime.Day.ToString().PadLeft(2, '0'), VbStrConv.Wide),
                Strings.StrConv(operatorInfo.TradeTime.Hour.ToString().PadLeft(2, '0'), VbStrConv.Wide),
                Strings.StrConv(operatorInfo.TradeTime.Minute.ToString().PadLeft(2, '0'), VbStrConv.Wide));
            data.Add(line02);

            // 3行目 駅コード、コーナー、号機Ｎｏ
            string line03 = StationNameEdit(operatorInfo.TradeTime);
            data.Add(line03);

            // 4行目 ブランク
            string line04 = string.Empty;
            data.Add(line04);

            // 5行目 補給合計
            string line05 = string.Format("入金合計　　　　　　　{0}円", Strings.StrConv((operatorInfo.TotalizeBill.B1000 * 1000).ToString().PadLeft(6), VbStrConv.Wide));
            data.Add(line05);

            // 6行目 紙幣補給枚数
            string line06 = string.Format("　　１０００円　　　　{0}枚", Strings.StrConv(operatorInfo.TotalizeBill.B1000.ToString().PadLeft(6), VbStrConv.Wide));
            data.Add(line06);

            // 7行目 ブランク
            string line07 = string.Empty;
            data.Add(line07);

            // 8行目 ブランク
            string line08 = string.Empty;
            data.Add(line08);

            // 9行目 補給回数
            string line09 = string.Empty;
            var usingData = new TotalizeDataSearch(TotalizeDataCount.GetInstance().GetData(DataCountBase.Generation.Cur, TotalizeDataCount.TotalizeKind.Day, DataCountLabels.CategoryCashControl));
            long count = usingData.GetData(DataCountLabels.CcPlusCount, 6);
            line09 = string.Format("入金回数　　　　　　　{0}回", Strings.StrConv(count.ToString().PadLeft(6), VbStrConv.Wide));
            data.Add(line09);

            // 10行目 補給累計金額
            string line10 = string.Empty;
            long supply1000 = usingData.GetData(DataCountLabels.CcSupNum01000y, 8);
            line10 = string.Format("入金累計　　　　　{0}円", Strings.StrConv((supply1000 * 1000).ToString().PadLeft(8), VbStrConv.Wide));
            data.Add(line10);
        }

        /// <summary>
        /// 印字情報作成処理（出金分納）
        /// </summary>
        /// <param name="data">印字情報</param>
        private void MakePrintDataForSplitMoney(ref List<string> data)
        {
            // 1行目 タイトル
            string line01 = "出金データ　　　　　　　　　　　　　";
            data.Add(line01);

            // 2行目 日時（西暦）
            string line02 = string.Format(
                "{0}年{1}月{2}日　{3}：{4}",
                Strings.StrConv(operatorInfo.TradeTime.Year.ToString(), VbStrConv.Wide),
                Strings.StrConv(operatorInfo.TradeTime.Month.ToString().PadLeft(2, '0'), VbStrConv.Wide),
                Strings.StrConv(operatorInfo.TradeTime.Day.ToString().PadLeft(2, '0'), VbStrConv.Wide),
                Strings.StrConv(operatorInfo.TradeTime.Hour.ToString().PadLeft(2, '0'), VbStrConv.Wide),
                Strings.StrConv(operatorInfo.TradeTime.Minute.ToString().PadLeft(2, '0'), VbStrConv.Wide));
            data.Add(line02);

            // 3行目 駅コード、コーナー、号機Ｎｏ
            string line03 = StationNameEdit(operatorInfo.TradeTime);
            data.Add(line03);

            // 4行目 ブランク
            string line04 = string.Empty;
            data.Add(line04);

            // 5行目 出金合計
            string line05 = string.Format("出金合計　　　　　　　{0}円", Strings.StrConv((operatorInfo.SplitBill.Total + operatorInfo.SplitCoin.Total).ToString().PadLeft(6), VbStrConv.Wide));
            data.Add(line05);

            // 6行目 紙幣金額
            string line06 = string.Format("紙幣金額　　　　　　　{0}円", Strings.StrConv(operatorInfo.SplitBill.Total.ToString().PadLeft(6), VbStrConv.Wide));
            data.Add(line06);

            // 7行目  紙幣 10000円
            // 8行目  紙幣  5000円
            // 9行目  紙幣  2000円
            // 10行目 紙幣  1000円
            string line07 = string.Format("　１００００円　　　　{0}枚", Strings.StrConv(operatorInfo.SplitBill.B10000.ToString().PadLeft(6), VbStrConv.Wide));
            string line08 = string.Format("　　５０００円　　　　{0}枚", Strings.StrConv(operatorInfo.SplitBill.B5000.ToString().PadLeft(6), VbStrConv.Wide));
            string line09 = string.Format("　　２０００円　　　　{0}枚", Strings.StrConv(operatorInfo.SplitBill.B2000.ToString().PadLeft(6), VbStrConv.Wide));
            string line10 = string.Format("　　１０００円　　　　{0}枚", Strings.StrConv(operatorInfo.SplitBill.B1000.ToString().PadLeft(6), VbStrConv.Wide));
            data.Add(line07);
            data.Add(line08);
            data.Add(line09);
            data.Add(line10);

            // 11行目 硬貨金額
            // 12行目 硬貨 500円
            // 13行目 硬貨 100円
            // 14行目 硬貨  50円
            // 15行目 硬貨  10円
            string line11 = string.Empty;
            string line12 = string.Empty;
            string line13 = string.Empty;
            string line14 = string.Empty;
            string line15 = string.Empty;
            if (settings.CHConnect.Value)
            {
                line11 = string.Format("硬貨金額　　　　　　　{0}円", Strings.StrConv(operatorInfo.SplitCoin.Total.ToString().PadLeft(6), VbStrConv.Wide));
                line12 = string.Format("　　　５００円　　　　{0}枚", Strings.StrConv(operatorInfo.SplitCoin.C500.ToString().PadLeft(6), VbStrConv.Wide));
                line13 = string.Format("　　　１００円　　　　{0}枚", Strings.StrConv(operatorInfo.SplitCoin.C100.ToString().PadLeft(6), VbStrConv.Wide));
                line14 = string.Format("　　　　５０円　　　　{0}枚", Strings.StrConv(operatorInfo.SplitCoin.C50.ToString().PadLeft(6), VbStrConv.Wide));
                line15 = string.Format("　　　　１０円　　　　{0}枚", Strings.StrConv(operatorInfo.SplitCoin.C10.ToString().PadLeft(6), VbStrConv.Wide));
            }
            else
            {
                line11 = "硬貨金額　　　　　　　　　　　　　－";
                line12 = "　　　５００円　　　　　　　　　　－";
                line13 = "　　　１００円　　　　　　　　　　－";
                line14 = "　　　　５０円　　　　　　　　　　－";
                line15 = "　　　　１０円　　　　　　　　　　－";
            }
            data.Add(line11);
            data.Add(line12);
            data.Add(line13);
            data.Add(line14);
            data.Add(line15);

            // 16行目 ブランク
            string line16 = string.Empty;
            data.Add(line16);

            // 17行目 出金回数
            string line17 = string.Empty;
            var usingData = new TotalizeDataSearch(TotalizeDataCount.GetInstance().GetData(DataCountBase.Generation.Cur, TotalizeDataCount.TotalizeKind.Day, DataCountLabels.CategoryCashControl));
            long count = usingData.GetData(DataCountLabels.CcInstallmentCount, 6);
            line17 = string.Format("出金回数　　　　　　　{0}回", Strings.StrConv(count.ToString().PadLeft(6), VbStrConv.Wide));
            data.Add(line17);

            // 18行目 出金累計金額
            string line18 = string.Empty;
            string[] dataCountLabels = { DataCountLabels.CcIsupNum00010y, DataCountLabels.CcIsupNum00050y, DataCountLabels.CcIsupNum00100y, DataCountLabels.CcIsupNum00500y, DataCountLabels.CcIsupNum01000y, DataCountLabels.CcIsupNum02000y, DataCountLabels.CcIsupNum05000y, DataCountLabels.CcIsupNum10000y };
            int[] kind = { 10, 50, 100, 500, 1000, 2000, 5000, 10000 };
            long splitNum = 0;
            long total = 0;
            for (int i = 0; i < dataCountLabels.Length; ++i)
            {
                splitNum = usingData.GetData(dataCountLabels[i], 6);
                total += splitNum * kind[i];
            }
            line18 = string.Format("出金累計　　　　　{0}円", Strings.StrConv(total.ToString().PadLeft(8), VbStrConv.Wide));
            data.Add(line18);
        }

        /// <summary>
        /// 印字情報作成処理（紙幣満杯回収）
        /// </summary>
        /// <param name="data">印字情報</param>
        private void MakePrintDataForCollectBillFull(ref List<string> data)
        {
            // 1行目 タイトル
            string line01 = "紙幣満杯回収データ　　　　　　　　　";
            data.Add(line01);

            // 2行目 日時（西暦）
            string line02 = string.Format(
                "{0}年{1}月{2}日　{3}：{4}",
                Strings.StrConv(operatorInfo.TradeTime.Year.ToString(), VbStrConv.Wide),
                Strings.StrConv(operatorInfo.TradeTime.Month.ToString().PadLeft(2, '0'), VbStrConv.Wide),
                Strings.StrConv(operatorInfo.TradeTime.Day.ToString().PadLeft(2, '0'), VbStrConv.Wide),
                Strings.StrConv(operatorInfo.TradeTime.Hour.ToString().PadLeft(2, '0'), VbStrConv.Wide),
                Strings.StrConv(operatorInfo.TradeTime.Minute.ToString().PadLeft(2, '0'), VbStrConv.Wide));
            data.Add(line02);

            // 3行目 駅コード、コーナー、号機Ｎｏ
            string line03 = StationNameEdit(operatorInfo.TradeTime);
            data.Add(line03);

            // 4行目 ブランク
            string line04 = string.Empty;
            data.Add(line04);

            // 5行目 分納合計
            string line05 = string.Format("回収合計　　　　　　　{0}円", Strings.StrConv(operatorInfo.TotalizeBill.Total.ToString().PadLeft(6), VbStrConv.Wide));
            data.Add(line05);

            // 6行目  紙幣 10000円
            // 7行目  紙幣  5000円
            // 8行目  紙幣  2000円
            // 9行目 紙幣  1000円
            // 10行目 金種不明
            string line06 = string.Format("　１００００円　　　　{0}枚", Strings.StrConv(operatorInfo.TotalizeBill.B10000.ToString().PadLeft(6), VbStrConv.Wide));
            string line07 = string.Format("　　５０００円　　　　{0}枚", Strings.StrConv(operatorInfo.TotalizeBill.B5000.ToString().PadLeft(6), VbStrConv.Wide));
            string line08 = string.Format("　　２０００円　　　　{0}枚", Strings.StrConv(operatorInfo.TotalizeBill.B2000.ToString().PadLeft(6), VbStrConv.Wide));
            string line09 = string.Format("　　１０００円　　　　{0}枚", Strings.StrConv(operatorInfo.TotalizeBill.B1000.ToString().PadLeft(6), VbStrConv.Wide));
            string line10 = string.Format("　　　金種不明　　　　{0}枚", Strings.StrConv(operatorInfo.TotalizeOther.ToString().PadLeft(6), VbStrConv.Wide));
            data.Add(line06);
            data.Add(line07);
            data.Add(line08);
            data.Add(line09);
            data.Add(line10);

            // 11行目 ブランク
            string line11 = string.Empty;
            data.Add(line11);

            // 12行目 累計回数
            string line12 = string.Empty;
            var usingData = new TotalizeDataSearch(TotalizeDataCount.GetInstance().GetData(DataCountBase.Generation.Cur, TotalizeDataCount.TotalizeKind.Day, DataCountLabels.CategoryCashControl));
            long count = usingData.GetData(DataCountLabels.CcFulloutCount, 6);
            line12 = string.Format("累計回数　　　　　　　{0}回", Strings.StrConv(count.ToString().PadLeft(6), VbStrConv.Wide));
            data.Add(line12);

            // 13行目 累計金額
            string line13 = string.Empty;
            string[] dataCountLabels = { DataCountLabels.CcFulloutNum01000y, DataCountLabels.CcFulloutNum02000y, DataCountLabels.CcFulloutNum05000y, DataCountLabels.CcFulloutNum10000y };
            int[] kind = { 1000, 2000, 5000, 10000 };
            long splitNum = 0;
            long total = 0;
            for (int i = 0; i < dataCountLabels.Length; ++i)
            {
                splitNum = usingData.GetData(dataCountLabels[i], 6);
                total += splitNum * kind[i];
            }
            line13 = string.Format("累計金額　　　　　{0}円", Strings.StrConv(total.ToString().PadLeft(8), VbStrConv.Wide));
            data.Add(line13);
        }

        /// <summary>
        /// 定額残置回収帳票編集
        /// </summary>
        /// <param name="data">印字情報</param>
        private void MakePrintDataForReserveCollect(ref List<string> data)
        {
            // 1行目 タイトル
            string line01 = "定額残置回収結果　　　　　　　　　　";
            data.Add(line01);

            // 2行目 日時（西暦）
            string line02 = string.Format(
                "{0}年{1}月{2}日　{3}：{4}",
                Strings.StrConv(operatorInfo.TradeTime.Year.ToString(), VbStrConv.Wide),
                Strings.StrConv(operatorInfo.TradeTime.Month.ToString().PadLeft(2, '0'), VbStrConv.Wide),
                Strings.StrConv(operatorInfo.TradeTime.Day.ToString().PadLeft(2, '0'), VbStrConv.Wide),
                Strings.StrConv(operatorInfo.TradeTime.Hour.ToString().PadLeft(2, '0'), VbStrConv.Wide),
                Strings.StrConv(operatorInfo.TradeTime.Minute.ToString().PadLeft(2, '0'), VbStrConv.Wide));
            data.Add(line02);

            // 3行目 駅コード、コーナー、号機Ｎｏ
            string line03 = StationNameEdit(operatorInfo.TradeTime);
            data.Add(line03);

            // 4行目 ブランク
            string line04 = string.Empty;
            data.Add(line04);

            // ５行目 タイトル
            string line05 = "　　　　　　　　　　　　　残置枚数";
            data.Add(line05);

            // 6行目  硬貨 10円
            // 8行目  硬貨 50円
            // 10行目 硬貨 100円
            // 12行目 硬貨 500円
            // 14行目 紙幣 1000円
            // 16行目 合計金額
            string line06 = string.Empty;
            string line08 = string.Empty;
            string line10 = string.Empty;
            string line12 = string.Empty;
            if (settings.CHConnect.Value)
            {
                line06 = string.Format("　　　　１０円　　　　　　{0}枚", Strings.StrConv(operatorInfo.CollectSaveData.ReserveResultCoin.Coin10.ToString().PadLeft(4), VbStrConv.Wide));
                line08 = string.Format("　　　　５０円　　　　　　{0}枚", Strings.StrConv(operatorInfo.CollectSaveData.ReserveResultCoin.Coin50.ToString().PadLeft(4), VbStrConv.Wide));
                line10 = string.Format("　　　１００円　　　　　　{0}枚", Strings.StrConv(operatorInfo.CollectSaveData.ReserveResultCoin.Coin100.ToString().PadLeft(4), VbStrConv.Wide));
                line12 = string.Format("　　　５００円　　　　　　{0}枚", Strings.StrConv(operatorInfo.CollectSaveData.ReserveResultCoin.Coin500.ToString().PadLeft(4), VbStrConv.Wide));
            }
            else
            {
                line06 = "　　　　１０円　　　　　　　　　　－";
                line08 = "　　　　５０円　　　　　　　　　　－";
                line10 = "　　　１００円　　　　　　　　　　－";
                line12 = "　　　５００円　　　　　　　　　　－";
            }
            string line14 = string.Format("　　１０００円　　　　　　{0}枚", Strings.StrConv(operatorInfo.CollectSaveData.ReserveResultBill.Bill1000.ToString().PadLeft(4), VbStrConv.Wide));

            ulong totalReserveAmount =
                (ulong)(operatorInfo.CollectSaveData.ReserveResultCoin.Coin10 * 10) +
                (ulong)(operatorInfo.CollectSaveData.ReserveResultCoin.Coin50 * 50) +
                (ulong)(operatorInfo.CollectSaveData.ReserveResultCoin.Coin100 * 100) +
                (ulong)(operatorInfo.CollectSaveData.ReserveResultCoin.Coin500 * 500) +
                (ulong)(operatorInfo.CollectSaveData.ReserveResultBill.Bill1000 * 1000);
            string line16 = string.Format("合計金額　　　　　　　{0}円", Strings.StrConv(totalReserveAmount.ToString().PadLeft(6), VbStrConv.Wide));

            data.Add(line06);
            data.Add(string.Empty);
            data.Add(line08);
            data.Add(string.Empty);
            data.Add(line10);
            data.Add(string.Empty);
            data.Add(line12);
            data.Add(string.Empty);
            data.Add(line14);
            data.Add(string.Empty);
            data.Add(line16);
        }

        /// <summary>
        /// 処理未了履歴印字：帳票データ作成
        /// </summary>
        /// <returns>印字帳票</returns>
        private List<string> MakePrintDataForWriteUnfinishedCollect()
        {
            var data = new List<string>();            
            string closingLastTimeString = TotalizeDataCount.GetInstance().GetClosingDate(DataCountBase.Generation.Old1, TotalizeDataCount.TotalizeKind.Day);
            string closingBeforeLastTimeString = TotalizeDataCount.GetInstance().GetClosingDate(DataCountBase.Generation.Old2, TotalizeDataCount.TotalizeKind.Day);

            // 処理未了履歴の取引データ取得処理
            List<SessionSaveTable> sessionListLast, sessionListBeforeLast;
            UnFinishWriteHistory.Read(out sessionListLast, out sessionListBeforeLast);

            // タイトル
            data.Add("　　　ＩＣカード処理未了履歴　　　　");
            data.Add(string.Empty);
            // 駅コード、コーナー、号機Ｎｏ
            data.Add(StationNameEdit(DateTime.Now));            
               
            // 現在～前回締切(cur)
            if (sessionListLast.Count() > 0)
            {
                // 前回締切時刻を取得（※締切未実施の場合は最古の未了取引時刻を取得）                
                DateTime firstTimePoint = !string.IsNullOrEmpty(closingLastTimeString) ?
                                           DateTime.ParseExact(closingLastTimeString, "yyyyMMddHHmmss", System.Globalization.CultureInfo.CurrentCulture) :
                                           sessionListLast[sessionListLast.Count - 1].StartTime.ConvertToDateTime();
                // 現在時刻を取得
                DateTime nowTime = DateTime.Now;
                // データ期間
                data.Add(string.Empty);
                data.Add("■■■■■　データ期間１　■■■■■");
                data.Add(string.Format("{0:D4}年{1:D2}月{2:D2}日　{3:D2}:{4:D2}　から", firstTimePoint.Year, firstTimePoint.Month, firstTimePoint.Day, firstTimePoint.Hour, firstTimePoint.Minute));
                data.Add(string.Format("{0:D4}年{1:D2}月{2:D2}日　{3:D2}:{4:D2}　まで", nowTime.Year, nowTime.Month, nowTime.Day, nowTime.Hour, nowTime.Minute));                
                data.Add(string.Empty);
                // 取引情報書込み
                MakePrintSessionDataForWriteUnfinished(data, sessionListLast);
            }

            // 前回締切～前々回締切(old1)
            if (!string.IsNullOrEmpty(closingLastTimeString) && sessionListBeforeLast.Count() > 0)
            {
                // 前々回締切時刻を取得（※締切未実施の場合は最古の未了取引時刻を取得）   
                DateTime secondTimePoint = !string.IsNullOrEmpty(closingBeforeLastTimeString) ?
                                           DateTime.ParseExact(closingBeforeLastTimeString, "yyyyMMddHHmmss", System.Globalization.CultureInfo.CurrentCulture) :
                                           sessionListBeforeLast[sessionListBeforeLast.Count - 1].StartTime.ConvertToDateTime();
                // 前回締切時刻を取得             
                DateTime firstTimePoint = DateTime.ParseExact(closingLastTimeString, "yyyyMMddHHmmss", System.Globalization.CultureInfo.CurrentCulture);
                // データ期間
                data.Add(string.Empty);
                data.Add("■■■■■　データ期間２　■■■■■");
                data.Add(string.Format("{0:D4}年{1:D2}月{2:D2}日　{3:D2}:{4:D2}　から", secondTimePoint.Year, secondTimePoint.Month, secondTimePoint.Day, secondTimePoint.Hour, secondTimePoint.Minute));
                data.Add(string.Format("{0:D4}年{1:D2}月{2:D2}日　{3:D2}:{4:D2}　まで", firstTimePoint.Year, firstTimePoint.Month, firstTimePoint.Day, firstTimePoint.Hour, firstTimePoint.Minute));                
                data.Add(string.Empty);
                // 取引情報書込み
                MakePrintSessionDataForWriteUnfinished(data, sessionListBeforeLast);
            }
            return data;
        }

        /// <summary>
        /// 印字用の文字列作成（取引）
        /// </summary>
        /// <param name="output">印字用文字列</param>
        /// <param name="sessionList">取引情報</param>
        private void MakePrintSessionDataForWriteUnfinished(List<string> output, List<SessionSaveTable> sessionList)
        {
            foreach (var sessionData in sessionList)
            {
                output.Add("＝＝＝＝＝＝＝＝＝＝＝＝＝＝＝＝＝＝");
                // 取扱日時
                output.Add(string.Empty);
                output.Add(string.Format("取扱日時：　{0:D4}年{1:D2}月{2:D2}日{3:D2}:{4:D2}", sessionData.StartTime.Year, sessionData.StartTime.Month, sessionData.StartTime.Day, sessionData.StartTime.Hour, sessionData.StartTime.Minute));             
                // ＩＤｉ                
                output.Add(string.Empty);
                string idiText = "カード番号：";
                if (sessionData.ReadCardList.Length > 0)
                {
                    string idi = Strings.StrConv(sessionData.ReadCardList[0].PrintIdi, VbStrConv.Narrow);
                    if (idi.Length > 8)
                    {
                        idiText = "カード番号：" + idi.Substring(0, 2) + "*******" + idi.Substring(idi.Length - 8, 8);
                    }
                }
                output.Add(idiText);

                // 券売機モードごとの編集を行う
                if (sessionData.Operation == Session.OperationType.Charge ||
                    sessionData.Operation == Session.OperationType.TenkeyCharge)
                {
                    // IC専用機(チャージorテンキーチャージ)
                    MakePrintSessionDataForWriteUnfinishedForICType(output, sessionData);
                }
                else
                {
                    // グリーン券売機（据置/スタンドアロン）
                    MakePrintSessionDataForWriteUnfinishedForGreenType(output, sessionData);
                }
            }
            output.Add("＝＝＝＝＝＝＝＝＝＝＝＝＝＝＝＝＝＝");
        }

        /// <summary>
        /// 印字用の文字列作成（取引）グリーン券売機
        /// </summary>
        /// <param name="output">印字用文字列</param>
        /// <param name="sessionData">取引情報</param>
        private void MakePrintSessionDataForWriteUnfinishedForGreenType(List<string> output, SessionSaveTable sessionData)
        {
            // 投入金額
            output.Add(string.Empty);
            output.Add(string.Format("投入金額：　{0}円", Strings.StrConv(sessionData.ChargeAmount.ToString().PadLeft(4), VbStrConv.Wide)));
            // 取引内容
            output.Add(string.Empty);
            switch (sessionData.Operation)
            {
                case Session.OperationType.GreenICAndCharge:
                    output.Add("取引内容：　チャージ＋グリーン券");
                    break;
                case Session.OperationType.GreenIC:
                    output.Add("取引内容：　グリーン券購入");
                    break;
                default:
                    output.Add("取引内容：");
                    break;
            }
            // カード状態
            output.Add(string.Empty);
            if (sessionData.IsWriteCommandSendAfter)
            {
                output.Add("カード状態：カード書込み中抜取");
                output.Add("　　　　　　カード内容を確認して");
                output.Add("　　　　　　下さい");
            }
            else
            {
                output.Add("カード状態：カード書込み前抜取");
            }
            // 集計状態
            output.Add(string.Empty);
            output.Add("集計状態：　投入金は売上計上されて");
            output.Add("　　　　　　いません");
            output.Add(string.Empty);
        }

        /// <summary>
        /// 印字用の文字列作成（取引）IC専用機
        /// </summary>
        /// <param name="output">印字用文字列</param>
        /// <param name="sessionData">取引情報</param>
        private void MakePrintSessionDataForWriteUnfinishedForICType(List<string> output, SessionSaveTable sessionData)
        {
            // 投入金額
            output.Add(string.Empty);
            output.Add(string.Format("投入金額：　{0}円", Strings.StrConv(sessionData.PooledTotal.ToString().PadLeft(5), VbStrConv.Wide)));
            // チャージ額
            output.Add(string.Format("ﾁｬｰｼﾞ 額：　{0}円", Strings.StrConv(sessionData.PurchaseAmount.ToString().PadLeft(5), VbStrConv.Wide)));
            // おつり
            output.Add(string.Format("おつり　：　{0}円", Strings.StrConv(sessionData.LogicalChangeBills.Total.ToString().PadLeft(5), VbStrConv.Wide)));
            // 取引内容
            output.Add(string.Empty);
            output.Add("取引内容：　チャージ");
            // カード状態
            output.Add(string.Empty);
            if (sessionData.IsWriteCommandSendAfter)
            {
                output.Add("カード状態：カード書込み中抜取");
                output.Add("　　　　　　カード内容を確認して");
                output.Add("　　　　　　下さい");
            }
            else
            {
                output.Add("カード状態：カード書込み前抜取");
            }
            // 集計状態
            output.Add(string.Empty);
            if (sessionData.IsCancelled)
            {
                output.Add("集計状態：　投入金は返却済");
            }
            else
            {
                output.Add("集計状態：　投入金は売上計上されて");
                output.Add("　　　　　　いません");
            }
            output.Add(string.Empty);
        }

        /// <summary>
        /// 硬貨補給帳票情報作成
        /// </summary>
        /// <param name="data">データ</param>
        /// <returns>補給情報</returns>
        public byte[] SupplyHistoryPrintDataCreateData(SupplyCountData data)
        {
            byte[] ticketOutput = null;
            // 発券情報作成
            var TicketMediaInput = new MediaDataInput();
            var JournalInput = new JournalDataInput();
            var journalData = new JournalData();
            // 硬貨補給データ設定
            journalData.LineData = MakePrintDataForSupplyHistory(data).ToArray();
            List<JournalData> jourData = new List<JournalData>();
            jourData.Add(journalData);
            JournalInput.Data = jourData.ToArray();
            TicketMediaInput.Info = JournalInput.GetBytes();
            TicketMediaInput.EditKind = MediaDataInput.MediaEditKind.EditJournal;
            ticketOutput = TicketMediaInput.GetBytes();
            return ticketOutput;
        }

        /// <summary>
        /// 硬貨補給帳票編集
        /// </summary>
        /// <param name="supplyData">データ</param>
        /// <returns>印字情報</returns>
        private List<string> MakePrintDataForSupplyHistory(SupplyCountData supplyData)
        {
            var data = new List<string>();
            // 1行目 タイトル
            string line01 = "硬貨補給結果";
            data.Add(line01);

            // 2行目 日時（西暦）
            string line02 = string.Format(
                "{0}年{1}月{2}日　{3}：{4}",
                Strings.StrConv(supplyData.Date.Substring(0, 4).ToString(), VbStrConv.Wide),
                Strings.StrConv(supplyData.Date.Substring(5, 2).ToString(), VbStrConv.Wide),
                Strings.StrConv(supplyData.Date.Substring(8, 2).ToString(), VbStrConv.Wide),
                Strings.StrConv(supplyData.Date.Substring(11, 2).ToString(), VbStrConv.Wide),
                Strings.StrConv(supplyData.Date.Substring(14, 2).ToString(), VbStrConv.Wide));
            data.Add(line02);

            // tradeTimeを取得して、DateTimeに変更する
            string inputDate = supplyData.Date.Substring(0, 4) + supplyData.Date.Substring(5, 2) + supplyData.Date.Substring(8, 2) + supplyData.Date.Substring(11, 2) + supplyData.Date.Substring(14, 2) + supplyData.Date.Substring(17, 2) + supplyData.Date.Substring(20, 3);
            DateTime date = new DateTime();
            DateTime.TryParseExact(inputDate, "yyyyMMddHHmmssfff", System.Globalization.CultureInfo.CurrentCulture, 0, out date);

            // 3行目 駅コード、コーナー、号機Ｎｏ
            string line03 = StationNameEdit(date);
            data.Add(line03);

            // 4行目 ブランク
            string line04 = string.Empty;
            data.Add(line04);

            // 5行目 タイトル 合計金額
            string line05 = string.Format("硬貨補給合計　　　　　{0}円", Strings.StrConv(supplyData.SupplyResultCash.Total.ToString().PadLeft(6), VbStrConv.Wide));
            data.Add(line05);

            // 6行目 硬貨  10円
            // 7行目 硬貨  50円
            // 8行目 硬貨 100円
            // 9行目 硬貨 500円
            string line06 = string.Format("　　　　１０円　　　　{0}枚", Strings.StrConv(supplyData.SupplyResultCash.Coin10.ToString().PadLeft(6), VbStrConv.Wide));
            string line07 = string.Format("　　　　５０円　　　　{0}枚", Strings.StrConv(supplyData.SupplyResultCash.Coin50.ToString().PadLeft(6), VbStrConv.Wide));
            string line08 = string.Format("　　　１００円　　　　{0}枚", Strings.StrConv(supplyData.SupplyResultCash.Coin100.ToString().PadLeft(6), VbStrConv.Wide));
            string line09 = string.Format("　　　５００円　　　　{0}枚", Strings.StrConv(supplyData.SupplyResultCash.Coin500.ToString().PadLeft(6), VbStrConv.Wide));
            data.Add(line06);
            data.Add(line07);
            data.Add(line08);
            data.Add(line09);

            // 10行目 補給リジェクト枚数
            string line10 = string.Format("リジェクト硬貨　　　　{0}枚", Strings.StrConv(supplyData.ChargeRejCount.ToString().PadLeft(6), VbStrConv.Wide));
            data.Add(line10);

            // 11行目 ブランク
            // 12行目 ブランク
            string line11 = string.Empty;
            data.Add(line11);
            string line12 = string.Empty;
            data.Add(line12);

            // 13行目 補給方法
            string line13 = string.Format("硬貨補給方法　　　　　　{0}", Strings.StrConv(supplyData.Kind.ToString().PadLeft(6), VbStrConv.Wide));
            data.Add(line13);

            // 14行目 補給方法
            string line14 = string.Format("硬貨補給回数　　　　　{0}回", Strings.StrConv(supplyData.Count.ToString().PadLeft(6), VbStrConv.Wide));
            data.Add(line14);

            // 15行目 補給方法
            string line15 = string.Format("硬貨補給累計　　　{0}円", Strings.StrConv(supplyData.CoinTotal.ToString().PadLeft(8), VbStrConv.Wide));
            data.Add(line15);
            return data;
        }

        /// <summary>
        /// 現金管理帳票印字 3行目 駅コード、コーナー、号機Ｎｏ
        /// </summary>
        /// <param name="tradeTime">印字用日付</param>
        /// <returns>表示内容</returns>
        private string StationNameEdit(DateTime tradeTime)
        {
            string text = string.Empty;
            string stationName = string.Empty;
            string corner = string.Empty;
            string no = string.Empty;
            var maintenanceSettings = DataStore.GetInstance().MaintenanceSettings;
            var operationalDateTime = new OperationalDateTime(tradeTime);

            // 駅名(TH:フル駅名券面日本語　RH:フル駅名レシート日本語)
            stationName = maintenanceSettings.THConnect.Value ?
                          UtilityInterface.GetStationInfo(maintenanceSettings.Line, maintenanceSettings.Station, UtilityInterface.StationNameFullTicketJpn, operationalDateTime.GetThreeHourDate()) :
                          UtilityInterface.GetStationInfo(maintenanceSettings.Line, maintenanceSettings.Station, UtilityInterface.StationNameFullReceiptJpn, operationalDateTime.GetThreeHourDate());

            if (stationName.Length != 0)
            {
                // 区切り文字削除
                stationName = stationName.Replace("｜", string.Empty);
                if (stationName.Length > 9)
                {
                    stationName = stationName.Remove(9);
                }
            }
            else
            {
                stationName = "　";
            }

            // コーナ
            corner = maintenanceSettings.FaceTicketCorner.Value;
            int cornerNo = int.Parse(corner);
            if (cornerNo >= 0 && cornerNo <= 9)
            {
                // 00～09の場合、「0」抜く一桁印字
                corner = cornerNo.ToString();
            }
            else if (cornerNo >= 10 && cornerNo <= 35)
            {
                // 10～35の場合、「A」～「Z」を転換して一桁印字
                char buf = (char)((int)'A' + cornerNo - 10);
                corner = buf.ToString();
            }
            else
            {
                // 上記以外の場合、ブランク印字
                corner = "　";
            }

            // 号機
            no = maintenanceSettings.FaceTicketNo.Value;

            // 3行目編集
            text = string.Format("　　　　　{0}　{1}{2}", stationName.PadRight(9, '　'), Strings.StrConv(corner, VbStrConv.Wide), Strings.StrConv(no, VbStrConv.Wide));

            return text;
        }
    }
}
