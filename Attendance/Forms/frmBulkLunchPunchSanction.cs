﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Text;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using DevExpress.XtraEditors;
using System.IO;
using System.Data.OleDb;
using System.Data.SqlClient;
using DevExpress.XtraGrid.Columns;
using Attendance.Classes;

namespace Attendance.Forms
{
    public partial class frmBulkLunchPunchSanction : DevExpress.XtraEditors.XtraForm
    {
        public string GRights = "XXXV";
        private int GFormID = 0;


        DataTable dt = new DataTable();

        public frmBulkLunchPunchSanction()
        {
            InitializeComponent();
            
        }

        
        private void btnBrowse_Click(object sender, EventArgs e)
        {
            OpenFileDialog openKeywordsFileDialog = new OpenFileDialog();
            openKeywordsFileDialog.InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            openKeywordsFileDialog.Multiselect = false;
            openKeywordsFileDialog.ValidateNames = true;
            //openKeywordsFileDialog.CheckFileExists = true;
            openKeywordsFileDialog.DereferenceLinks = false;        //Will return .lnk in shortcuts.
            openKeywordsFileDialog.Filter = "Files|*.xls;*.xlsx;*.xlsb";
            openKeywordsFileDialog.FileOk += new System.ComponentModel.CancelEventHandler(OpenKeywordsFileDialog_FileOk);
            var dialogResult = openKeywordsFileDialog.ShowDialog();

            if (dialogResult == System.Windows.Forms.DialogResult.OK)
            {
                //first check if already exits if found return..
                string filenm = openKeywordsFileDialog.FileName.ToString();
                if (string.IsNullOrEmpty(filenm))
                    return;
                try
                {
                    txtBrowse.Text = openKeywordsFileDialog.FileName;
                }
                catch (Exception ex)
                {
                    txtBrowse.Text = "";
                }
            }
            else
            {
                txtBrowse.Text = "";
            }
        }

        void OpenKeywordsFileDialog_FileOk(object sender, System.ComponentModel.CancelEventArgs e)
        {
            OpenFileDialog fileDialog = sender as OpenFileDialog;
            string selectedFile = fileDialog.FileName;
            if (string.IsNullOrEmpty(selectedFile) || selectedFile.Contains(".lnk"))
            {
                MessageBox.Show("Please select a valid File");
                e.Cancel = true;
            }
            return;
        }

        private string DataValidate(DataRow tdr)
        {
            string err = string.Empty;
            clsEmp t = new clsEmp();
            t.CompCode = "01";
            t.EmpUnqID = tdr["EmpUnqID"].ToString();
            if (!t.GetEmpDetails(t.CompCode,t.EmpUnqID))
            {
                err = err + "Invalid/InActive EmpUnqID..." + Environment.NewLine;
            }

            return err;
        }


        private void btnImport_Click(object sender, EventArgs e)
        {

            Cursor.Current = Cursors.WaitCursor;
            DataTable LunchMachines = Utils.Helper.GetData("Select * from [LunchMachine]",Utils.Helper.constr).Tables[0];
            DataTable dtMaterial = new DataTable();
            DataTable sortedDT = new DataTable();
            try
            {

                foreach (GridColumn column in grd_view1.VisibleColumns)
                {
                    if (column.FieldName != string.Empty)
                        dtMaterial.Columns.Add(column.FieldName, column.ColumnType);
                }


                for (int i = 0; i < grd_view1.DataRowCount; i++)
                {
                    DataRow row = dtMaterial.NewRow();

                    foreach (GridColumn column in grd_view1.VisibleColumns)
                    {
                        row[column.FieldName] = grd_view1.GetRowCellValue(i, column);
                    }
                    dtMaterial.Rows.Add(row);
                }

                DataView dv = dtMaterial.DefaultView;
                dv.Sort = "EmpUnqID asc";
                sortedDT = dv.ToTable();

                using (SqlConnection con = new SqlConnection(Utils.Helper.constr))
                {
                    DateTime tdt;

                    con.Open();
                    foreach (DataRow dr in sortedDT.Rows)
                    {
                        string tEmpUnqID = dr["EmpUnqID"].ToString();
                        

                        try
                        {
                            tdt = Convert.ToDateTime(dr["SanDate"]);
                        }
                        catch (Exception ex)
                        {
                            dr["Remarks"] = "Sanction Date Conversion failed...";
                            continue; 
                        }
                        
                        string err = DataValidate(dr);

                        if (!string.IsNullOrEmpty(err))
                        {
                            dr["Remarks"] = err;
                            continue; 
                        }

                        if(string.IsNullOrEmpty(dr["Location"].ToString().Trim()))
                        {
                            dr["Remarks"] = "Location Must be Required..";
                            continue;
                        }

                        clsEmp Emp = new clsEmp();

                        #region Chk_Primary

                        try
                        {
                            Emp.CompCode = "01";
                            Emp.EmpUnqID = tEmpUnqID;
                            Emp.GetEmpDetails(Emp.CompCode,Emp.EmpUnqID);

                            
                        }
                        catch (Exception ex)
                        {
                            dr["Remarks"] = ex.ToString();
                            continue;
                        }
                        #endregion

                        #region Chk_AllVals
                        //check all values if all empty skip
                        if(dr["LunchTime"].ToString().Trim() == "" )
                        {
                            dr["Remarks"] = dr["Remarks"].ToString() + " Nothing to update...";
                            continue;
                        }
                        #endregion
                        
                        DateTime tInTime = new DateTime(), tDate = new DateTime();
                        DateTime tSanDate = Convert.ToDateTime(dr["SanDate"]).Date;
                        #region Chk_InTime
                        if (dr["LunchTime"].ToString().Trim() != "" && dr["SanDate"] != DBNull.Value)
                        {
                            
                            tInTime = Convert.ToDateTime(dr["SanDate"]);
                            string[] inary = dr["LunchTime"].ToString().Split(':');

                            if(inary.GetLength(0) >= 2)
                            {
                               tInTime = tInTime.AddHours(Convert.ToInt32(inary[0].ToString().Trim()));
                               tInTime = tInTime.AddMinutes(Convert.ToInt32(inary[1].ToString().Trim()));
                            }else
                            {
                                dr["LunchTime"] = DBNull.Value;
                                dr["Remarks"] = dr["Remarks"].ToString() + " Invalid InTime, will not be considered..";
                                continue;
                            }

                        }
                        #endregion


                        #region Chk_AllVals
                        //check all values if all empty skip
                        if(dr["LunchTime"].ToString().Trim() == ""  )
                        {
                            dr["Remarks"] = dr["Remarks"].ToString() + " Nothing to update...";
                            continue;
                        }
                        
                        string sWrkGrp = "",sDate = "", sInTime = "", sLocation = "",sInMachine = "" ;
                        
                        sWrkGrp = Emp.WrkGrp;

                        sLocation = dr["Location"].ToString().Trim();

                        
                        string expression;
                        expression = "Location = '" + sLocation + "'";
                        DataRow[] foundRows;
                        foundRows = LunchMachines.Select(expression);
                        if(foundRows.Length <= 0){
                             dr["Remarks"] = dr["Remarks"].ToString() + " Invalid Location...";
                            continue;
                        }else{
                            sInMachine = foundRows[0]["InMachine"].ToString();
                            
                        }

                        #endregion


                        #region Set_InTime
                        if (tInTime == DateTime.MinValue || tInTime == Convert.ToDateTime(dr["SanDate"]))
                        {
                            sInTime = " NULL " ;
                        }else if( tInTime.Hour > 0 || tInTime.Minute >= 0){
                            sInTime = "'" + tInTime.ToString("yyyy-MM-dd HH:mm:ss") + "'";
                        }else{
                            sInTime = " NULL " ;
                        }
                        #endregion



                        #region Final_Update
                        

                        string sql = string.Empty;
                        

                        using (SqlCommand cmd = new SqlCommand())
                        {                            
                            try
                            {
                                sDate = Convert.ToDateTime(dr["SanDate"]).ToString("yyyy-MM-dd");
                                tDate = Convert.ToDateTime(dr["SanDate"]);
                                sql = string.Empty;

                                cmd.Connection = con;
                                cmd.CommandType = CommandType.Text;
                                if (!string.IsNullOrEmpty(sInTime) && sInTime != " NULL ")
                                {
                                    sql = "Insert Into AttdLog " +
                                    " (PunchDate,EmpUnqID,IOFLG,MachineIP,LunchFlg,tYear,tYearMt,t1Date,AddDt,AddID) Values (" +
                                    " " + sInTime + ",'" + Emp.EmpUnqID + "','B','" + sInMachine + "',1,'" + tDate.Year + "','" + tDate.ToString("yyyyMM") + "','" + sDate + "',GetDate(),'" + Utils.User.GUserID + "-San" + "')";
                                }
                                
                                cmd.CommandText = sql;
                                cmd.CommandTimeout = 0;
                                cmd.ExecuteNonQuery();



                                clsProcess pro = new clsProcess();
                                int result = 0;
                                string proerr = string.Empty;
                                pro.LunchProcess(Emp.EmpUnqID, tDate, tDate.AddDays(1), out result);
                                dr["remarks"] = dr["remarks"].ToString() + "Record updated...";
                                
                                

                            }
                            catch (Exception ex)
                            {
                                dr["remarks"] = dr["remarks"].ToString() + ex.ToString();
                                continue;
                            }

                        }//using sqlcommand
                        #endregion
                    }//using foreach

                    con.Close();
                }//using connection

                Cursor.Current = Cursors.Default;
                MessageBox.Show("file uploaded Successfully, please check the remarks for indivisual record status...", "Info", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex) { MessageBox.Show(ex.ToString(), "Error", MessageBoxButtons.OK, MessageBoxIcon.Error); }
            DataSet ds = new DataSet();
            ds.Tables.Add(sortedDT);
            grd_view.DataSource = ds;
            grd_view.DataMember = ds.Tables[0].TableName;
            grd_view.Refresh();

            Cursor.Current = Cursors.Default;
        }

        private void btnPreview_Click(object sender, EventArgs e)
        {
            if (string.IsNullOrEmpty(txtBrowse.Text.Trim().ToString()))
            {
                MessageBox.Show("Please Select Excel File First...", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            btnBrowse.Enabled = false;

            if (GRights.Contains("A") || GRights.Contains("U") || GRights.Contains("D"))
            {
                btnImport.Enabled = true;
            }
            else
            {
                btnImport.Enabled = false;
            }

            

            Cursor.Current = Cursors.WaitCursor;
            grd_view.DataSource = null;
            string filePath = txtBrowse.Text.ToString();

            string sexcelconnectionstring = @"Provider=Microsoft.ACE.OLEDB.12.0;Data Source=" + filePath + ";Extended Properties=\"Excel 12.0 Xml;HDR=YES;IMEX=1;\"";
            //string sexcelconnectionstring = @"provider=microsoft.jet.oledb.4.0;data source=" + filePath + ";extended properties=" + "\"excel 8.0;hdr=yes;IMEX=1;\"";

            OleDbConnection oledbconn = new OleDbConnection(sexcelconnectionstring);
            List<SheetName> sheets = ExcelHelper.GetSheetNames(oledbconn);
            string sheetname = "[" + sheets[0].sheetName.Replace("'", "") + "]";

            try
            {
                oledbconn.Open();
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.ToString(), "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }
            


            try
            {
                string myexceldataquery = "select EmpUnqID,SanDate,Location,LunchTime,'' as Remarks from " + sheetname;
                OleDbDataAdapter oledbda = new OleDbDataAdapter(myexceldataquery, oledbconn);
                dt.Clear();
                oledbda.Fill(dt);
                
                dt.AcceptChanges();
                foreach (DataRow row in dt.Rows)
                {
                    if (string.IsNullOrEmpty(row["EmpUnqID"].ToString().Trim()))
                        row.Delete();
                }
                dt.AcceptChanges();

                oledbconn.Close();
            }
            catch (Exception ex)
            {
                oledbconn.Close();
                MessageBox.Show("Please Check upload template.." + Environment.NewLine + ex.ToString(), "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                Cursor.Current = Cursors.Default;
                btnImport.Enabled = false;
                oledbconn.Close();
                return;
            }
            

            DataView dv = dt.DefaultView;
            dv.Sort = "EmpUnqID asc";
            DataTable sortedDT = dv.ToTable();




            grd_view.DataSource = sortedDT;

            if (GRights.Contains("A") || GRights.Contains("U") || GRights.Contains("D"))
            {
                btnImport.Enabled = true;
            }
            else
            {
                btnImport.Enabled = false;
            }

            Cursor.Current = Cursors.Default;
        }

        private void btnExport_Click(object sender, EventArgs e)
        {
            using (SaveFileDialog saveDialog = new SaveFileDialog())
            {
                saveDialog.Filter = "Excel (2003)(.xls)|*.xls|Excel (2010) (.xlsx)|*.xlsx |RichText File (.rtf)|*.rtf |Pdf File (.pdf)|*.pdf |Html File (.html)|*.html";
                if (saveDialog.ShowDialog() != DialogResult.Cancel)
                {
                    string exportFilePath = saveDialog.FileName;
                    string fileExtenstion = new FileInfo(exportFilePath).Extension;

                    switch (fileExtenstion)
                    {
                        case ".xls":
                            grd_view.ExportToXls(exportFilePath);
                            break;
                        case ".xlsx":
                            grd_view.ExportToXlsx(exportFilePath);
                            break;
                        case ".rtf":
                            grd_view.ExportToRtf(exportFilePath);
                            break;
                        case ".pdf":
                            grd_view.ExportToPdf(exportFilePath);
                            break;
                        case ".html":
                            grd_view.ExportToHtml(exportFilePath);
                            break;
                        case ".mht":
                            grd_view.ExportToMht(exportFilePath);
                            break;
                        default:
                            break;
                    }

                    if (File.Exists(exportFilePath))
                    {
                        try
                        {
                            //Try to open the file and let windows decide how to open it.
                            System.Diagnostics.Process.Start(exportFilePath);
                        }
                        catch
                        {
                            String msg = "The file could not be opened." + Environment.NewLine + Environment.NewLine + "Path: " + exportFilePath;
                            MessageBox.Show(msg, "Error!", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        }
                    }
                    else
                    {
                        String msg = "The file could not be saved." + Environment.NewLine + Environment.NewLine + "Path: " + exportFilePath;
                        MessageBox.Show(msg, "Error!", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                }
            }
        }

        private void frmBulkLunchPunchSanction_Load(object sender, EventArgs e)
        {
            GRights = Attendance.Classes.Globals.GetFormRights(this.Name);
            
            //string s = Utils.Helper.GetDescription("Select SanDayLimit from MastBCFlg", Utils.Helper.constr);
            //if(string.IsNullOrEmpty(s)){
            //    rSanDayLimit = 0;
            //    MessageBox.Show("Please Contact to Admin : for some confuguraiton required.","Error",MessageBoxButtons.OK,MessageBoxIcon.Error);
            //}else{
            //    rSanDayLimit = Convert.ToInt32(s);

            //    if(rSanDayLimit == 0)
            //    {
            //        MessageBox.Show("Please Contact to Admin : for some confuguraiton required.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            //    }
            //}

            GFormID = Convert.ToInt32(Utils.Helper.GetDescription("Select FormId from MastFrm Where FormName ='" + this.Name + "'",Utils.Helper.constr));

                
            grd_view.DataSource = null;
            btnImport.Enabled = false;
        }
    }
}