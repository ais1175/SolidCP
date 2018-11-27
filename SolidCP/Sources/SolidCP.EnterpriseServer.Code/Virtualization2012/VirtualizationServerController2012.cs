// Copyright (c) 2016, SolidCP
// SolidCP is distributed under the Creative Commons Share-alike license
// 
// SolidCP is a fork of WebsitePanel:
// Copyright (c) 2015, Outercurve Foundation.
// All rights reserved.
//
// Redistribution and use in source and binary forms, with or without modification,
// are permitted provided that the following conditions are met:
//
// - Redistributions of source code must  retain  the  above copyright notice, this
//   list of conditions and the following disclaimer.
//
// - Redistributions in binary form  must  reproduce the  above  copyright  notice,
//   this list of conditions  and  the  following  disclaimer in  the documentation
//   and/or other materials provided with the distribution.
//
// - Neither  the  name  of  the  Outercurve Foundation  nor   the   names  of  its
//   contributors may be used to endorse or  promote  products  derived  from  this
//   software without specific prior written permission.
//
// THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS "AS IS" AND
// ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING,  BUT  NOT  LIMITED TO, THE IMPLIED
// WARRANTIES  OF  MERCHANTABILITY   AND  FITNESS  FOR  A  PARTICULAR  PURPOSE  ARE
// DISCLAIMED. IN NO EVENT SHALL THE COPYRIGHT HOLDER OR CONTRIBUTORS BE LIABLE FOR
// ANY DIRECT, INDIRECT, INCIDENTAL,  SPECIAL,  EXEMPLARY, OR CONSEQUENTIAL DAMAGES
// (INCLUDING, BUT NOT LIMITED TO,  PROCUREMENT  OF  SUBSTITUTE  GOODS OR SERVICES;
// LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION)  HOWEVER  CAUSED AND ON
// ANY  THEORY  OF  LIABILITY,  WHETHER  IN  CONTRACT,  STRICT  LIABILITY,  OR TORT
// (INCLUDING NEGLIGENCE OR OTHERWISE)  ARISING  IN  ANY WAY OUT OF THE USE OF THIS
// SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.

using System;
using System.Data;
using System.Xml;
using SolidCP.Providers.Common;
using SolidCP.Providers.ResultObjects;
using SolidCP.Providers.Virtualization;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.IO;
using SolidCP.Providers;
using System.Text;
using System.Collections;
using System.Net.Mail;
using System.Diagnostics;
﻿using System.Linq;
﻿using System.Net;
﻿using SolidCP.EnterpriseServer.Code.Virtualization2012;
﻿using SolidCP.Providers.Virtualization2012;
using SolidCP.EnterpriseServer.Code.Virtualization2012.Helpers;
using System.Threading;

namespace SolidCP.EnterpriseServer
{
    public class VirtualizationServerController2012
    {
        private const string SHUTDOWN_REASON = "SolidCP - Initiated by user";
        private const string SHUTDOWN_REASON_CHANGE_CONFIG = "SolidCP - changing VPS configuration";
        private const Int64 Size1G = 0x40000000;
        private const string MS_MAC_PREFIX = "00155D"; // IEEE prefix of MS MAC addresses
        private const string MAINTENANCE_MODE_EMABLED = "enabled";

        // default server creation (if "Unlimited" was specified in the hosting plan)
        private const int DEFAULT_PASSWORD_LENGTH = 12;
        private const int DEFAULT_RAM_SIZE = 512; // megabytes
        private const int DEFAULT_HDD_SIZE = 20; // gigabytes
        private const int DEFAULT_PRIVATE_IPS_NUMBER = 1;
        private const int DEFAULT_SNAPSHOTS_NUMBER = 5;
        private const int DEFAULT_VLAN = 0;
        private const int DEFAULT_MINIMUM_IOPS = 0;
        private const int DEFAULT_MAXIMUM_IOPS = 0;

        #region Virtual Machines
        public static VirtualMachineMetaItemsPaged GetVirtualMachines(int packageId,
            string filterColumn, string filterValue, string sortColumn, int startRow, int maximumRows, bool recursive)
        {
            VirtualMachineMetaItemsPaged result = new VirtualMachineMetaItemsPaged();

            // get reader
            IDataReader reader = DataProvider.GetVirtualMachinesPaged2012(
                    SecurityContext.User.UserId,
                    packageId, filterColumn, filterValue, sortColumn, startRow, maximumRows, recursive);

            // number of items = first data reader
            reader.Read();
            result.Count = (int)reader[0];

            // items = second data reader
            reader.NextResult();
            result.Items = ObjectUtils.CreateListFromDataReader<VirtualMachineMetaItem>(reader).ToArray();

            return result;
        }

        public static VirtualMachine[] GetVirtualMachinesByServiceId(int serviceId)
        {
            // get proxy
            VirtualizationServer2012 vps = GetVirtualizationProxy(serviceId);

            // load details
            return vps.GetVirtualMachines();
        }
        #endregion

        #region Private Network
        public static PrivateIPAddressesPaged GetPackagePrivateIPAddressesPaged(int packageId,
            string filterColumn, string filterValue, string sortColumn, int startRow, int maximumRows)
        {
            PrivateIPAddressesPaged result = new PrivateIPAddressesPaged();

            // get reader
            IDataReader reader = DataProvider.GetPackagePrivateIPAddressesPaged(packageId, filterColumn, filterValue,
                sortColumn, startRow, maximumRows);

            // number of items = first data reader
            reader.Read();
            result.Count = (int)reader[0];

            // items = second data reader
            reader.NextResult();
            result.Items = ObjectUtils.CreateListFromDataReader<PrivateIPAddress>(reader).ToArray();

            return result;
        }

        public static List<PrivateIPAddress> GetPackagePrivateIPAddresses(int packageId)
        {
            return ObjectUtils.CreateListFromDataReader<PrivateIPAddress>(
                DataProvider.GetPackagePrivateIPAddresses(packageId));
        }
        #endregion

        #region User Permissions
        public static List<VirtualMachinePermission> GetSpaceUserPermissions(int packageId)
        {
            List<VirtualMachinePermission> result = new List<VirtualMachinePermission>();
            return result;
        }

        public static int UpdateSpaceUserPermissions(int packageId, VirtualMachinePermission[] permissions)
        {
            // VDC - UPDATE_PERMISSIONS
            return 0;
        }
        #endregion

        #region Audit Log
        public static List<LogRecord> GetSpaceAuditLog(int packageId, DateTime startPeriod, DateTime endPeriod,
            int severity, string sortColumn, int startRow, int maximumRows)
        {
            List<LogRecord> result = new List<LogRecord>();
            return result;
        }

        public static List<LogRecord> GetVirtualMachineAuditLog(int itemId, DateTime startPeriod, DateTime endPeriod,
            int severity, string sortColumn, int startRow, int maximumRows)
        {
            List<LogRecord> result = new List<LogRecord>();
            return result;
        }
        #endregion

        #region VPS Create – Name & OS
        public static LibraryItem[] GetOperatingSystemTemplates(int packageId)
        {
            // load service settings
            int serviceId = GetServiceId(packageId);

            // return templates
            return GetOperatingSystemTemplatesByServiceId(serviceId);
        }

        public static LibraryItem[] GetOperatingSystemTemplatesByServiceId(int serviceId)
        {
            // load service settings
            StringDictionary settings = ServerController.GetServiceSettings(serviceId);
            string xml = settings["OsTemplates"];

            var config = new ConfigFile(xml);

            return config.LibraryItems;
        }
        #endregion

        #region VPS Create - Configuration
        public static int GetMaximumCpuCoresNumber(int packageId)
        {
            // get proxy
            VirtualizationServer2012 vs = GetVirtualizationProxyByPackageId(packageId);

            return vs.GetProcessorCoresNumber();
        }

        public static string GetDefaultExportPath(int itemId)
        {
            // load meta item
            VirtualMachine vm = GetVirtualMachineByItemId(itemId);

            if (vm == null)
                return null;

            // load settings
            StringDictionary settings = ServerController.GetServiceSettings(vm.ServiceId);
            return settings["ExportedVpsPath"];
        }
        #endregion

        #region VPS Create
        public static IntResult CreateDefaultVirtualMachine(int packageId,
            string hostname, string osTemplate, string password, string summaryLetterEmail)
        {
            if (String.IsNullOrEmpty(osTemplate))
                throw new ArgumentNullException("osTemplate");

            IntResult res = new IntResult();

            VirtualMachine vmSettings = new VirtualMachine();

            // load package info
            PackageInfo package = PackageController.GetPackage(packageId);
            if (package == null)
            {
                res.ErrorCodes.Add("VPS_CREATE_PACKAGE_NOT_FOUND");
                return res;
            }

            // generate host name if not specified
            if (String.IsNullOrEmpty(hostname))
            {
                // load hostname pattern
                PackageSettings spaceSettings = PackageController.GetPackageSettings(packageId, PackageSettings.VIRTUAL_PRIVATE_SERVERS_2012);
                string hostnamePattern = spaceSettings["HostnamePattern"];
                if (String.IsNullOrEmpty(hostnamePattern))
                {
                    res.ErrorCodes.Add("VPS_CREATE_EMPTY_HOSTNAME_PATTERN");
                    return res;
                }

                hostname = EvaluateSpaceVariables(hostnamePattern, packageId);
            }

            // generate password if not specified
            if (String.IsNullOrEmpty(password))
            {
                int passwordLength = DEFAULT_PASSWORD_LENGTH; // default length

                // load password policy
                UserSettings userSettings = UserController.GetUserSettings(package.UserId, UserSettings.VPS_POLICY);
                string passwordPolicy = userSettings["AdministratorPasswordPolicy"];

                if (!String.IsNullOrEmpty(passwordPolicy))
                {
                    // get second parameter - max length
                    passwordLength = Utils.ParseInt(passwordPolicy.Split(';')[1].Trim(), passwordLength);
                }

                // generate password
                password = Utils.GetRandomString(passwordLength);
            }

            // load quotas
            PackageContext cntx = PackageController.GetPackageContext(packageId);
            if (cntx.Groups.ContainsKey(ResourceGroups.VPS2012))
            {
                res.ErrorCodes.Add("VPS_CREATE_VPS_GROUP_DISABLED");
                return res;
            }

            // CPU cores
            int cpuCores = cntx.Quotas[Quotas.VPS2012_CPU_NUMBER].QuotaAllocatedValue;
            if (cpuCores == -1) // unlimited is not possible
                cpuCores = GetMaximumCpuCoresNumber(packageId);

            // RAM
            int ramMB = cntx.Quotas[Quotas.VPS2012_RAM].QuotaAllocatedValue;
            if (ramMB == -1) // unlimited is not possible
                ramMB = DEFAULT_RAM_SIZE;

            // HDD
            int hddGB = cntx.Quotas[Quotas.VPS2012_HDD].QuotaAllocatedValue;
            if (hddGB == -1) // unlimited is not possible
                hddGB = DEFAULT_HDD_SIZE;

            // IOPS
            // TODO IOPS checks
            vmSettings.HddMinimumIOPS = DEFAULT_MINIMUM_IOPS;
            vmSettings.HddMaximumIOPS = DEFAULT_MAXIMUM_IOPS;

            // snapshots
            int snapshots = cntx.Quotas[Quotas.VPS2012_SNAPSHOTS_NUMBER].QuotaAllocatedValue;
            if (snapshots == -1) // unlimited is not possible
                snapshots = DEFAULT_SNAPSHOTS_NUMBER;

            bool dvdInstalled = !cntx.Quotas[Quotas.VPS2012_DVD_ENABLED].QuotaExhausted;
            bool bootFromCD = !cntx.Quotas[Quotas.VPS2012_BOOT_CD_ENABLED].QuotaExhausted;
            bool numLock = true;

            bool startShutdownAllowed = !cntx.Quotas[Quotas.VPS2012_START_SHUTDOWN_ALLOWED].QuotaExhausted;
            bool pauseResumeAllowed = !cntx.Quotas[Quotas.VPS2012_PAUSE_RESUME_ALLOWED].QuotaExhausted;
            bool rebootAllowed = !cntx.Quotas[Quotas.VPS2012_REBOOT_ALLOWED].QuotaExhausted;
            bool resetAllowed = !cntx.Quotas[Quotas.VPS2012_RESET_ALOWED].QuotaExhausted;
            bool reinstallAllowed = !cntx.Quotas[Quotas.VPS2012_REINSTALL_ALLOWED].QuotaExhausted;

            bool externalNetworkEnabled = !cntx.Quotas[Quotas.VPS2012_EXTERNAL_NETWORK_ENABLED].QuotaExhausted;
            int externalAddressesNumber = cntx.Quotas[Quotas.VPS2012_EXTERNAL_IP_ADDRESSES_NUMBER].QuotaAllocatedValue;
            bool randomExternalAddresses = true;
            int[] externalAddresses = new int[0]; // empty array
            if (externalNetworkEnabled)
            {
                int maxExternalAddresses = ServerController.GetPackageUnassignedIPAddresses(packageId, IPAddressPool.VpsExternalNetwork).Count;
                if (externalAddressesNumber == -1
                    || externalAddressesNumber > maxExternalAddresses)
                    externalAddressesNumber = maxExternalAddresses;
            }

            bool privateNetworkEnabled = !cntx.Quotas[Quotas.VPS2012_PRIVATE_NETWORK_ENABLED].QuotaExhausted;
            int privateAddressesNumber = cntx.Quotas[Quotas.VPS2012_PRIVATE_IP_ADDRESSES_NUMBER].QuotaAllocatedValue;
            bool randomPrivateAddresses = true;
            string[] privateAddresses = new string[0]; // empty array
            if (privateAddressesNumber == -1) // unlimited is not possible
            {
                privateAddressesNumber = DEFAULT_PRIVATE_IPS_NUMBER;
            }

            // create server and return result
            return CreateVirtualMachine(packageId, hostname, osTemplate, password, summaryLetterEmail,
                cpuCores, ramMB, hddGB, snapshots,
                dvdInstalled, bootFromCD, numLock,
                startShutdownAllowed, pauseResumeAllowed, rebootAllowed, resetAllowed, reinstallAllowed,
                externalNetworkEnabled, externalAddressesNumber, randomExternalAddresses, externalAddresses,
                privateNetworkEnabled, privateAddressesNumber, randomPrivateAddresses, privateAddresses, vmSettings);
        }

        public static IntResult CreateNewVirtualMachine(VirtualMachine VMSettings, string osTemplateFile, string password, string summaryLetterEmail, 
            int externalAddressesNumber, bool randomExternalAddresses, int[] externalAddresses,
            int privateAddressesNumber, bool randomPrivateAddresses, string[] privateAddresses)
        {
            // result object
            IntResult res = new IntResult();            

            // meta item
            VirtualMachine vm = null;

            int packageId = VMSettings.PackageId;
            try
            {
                #region Check account and space statuses
                // check account
                if (!SecurityContext.CheckAccount(res, DemandAccount.NotDemo | DemandAccount.IsActive))
                    return res;

                // check package
                if (!SecurityContext.CheckPackage(res, packageId, DemandPackage.IsActive))
                    return res;

                #endregion                

                #region Check Quotas
                // check quotas
                List<string> quotaResults = new List<string>();
                PackageContext cntx = PackageController.GetPackageContext(packageId);

                // dynamic memory
                var newRam = VMSettings.RamSize;
                if (VMSettings.DynamicMemory != null && VMSettings.DynamicMemory.Enabled)
                {
                    newRam = VMSettings.DynamicMemory.Maximum;

                    if (VMSettings.RamSize > VMSettings.DynamicMemory.Maximum || VMSettings.RamSize < VMSettings.DynamicMemory.Minimum)
                        quotaResults.Add(VirtualizationErrorCodes.QUOTA_NOT_IN_DYNAMIC_RAM);
                }

                QuotaHelper.CheckListsQuota(cntx, quotaResults, Quotas.VPS2012_SERVERS_NUMBER, VirtualizationErrorCodes.QUOTA_EXCEEDED_SERVERS_NUMBER);

                QuotaHelper.CheckNumericQuota(cntx, quotaResults, Quotas.VPS2012_CPU_NUMBER, VMSettings.CpuCores, VirtualizationErrorCodes.QUOTA_EXCEEDED_CPU);
                QuotaHelper.CheckNumericQuota(cntx, quotaResults, Quotas.VPS2012_RAM, newRam, VirtualizationErrorCodes.QUOTA_EXCEEDED_RAM);
                QuotaHelper.CheckNumericQuota(cntx, quotaResults, Quotas.VPS2012_HDD, VMSettings.HddSize, VirtualizationErrorCodes.QUOTA_EXCEEDED_HDD);
                QuotaHelper.CheckNumericQuota(cntx, quotaResults, Quotas.VPS2012_SNAPSHOTS_NUMBER, VMSettings.SnapshotsNumber, VirtualizationErrorCodes.QUOTA_EXCEEDED_SNAPSHOTS);

                QuotaHelper.CheckBooleanQuota(cntx, quotaResults, Quotas.VPS2012_DVD_ENABLED, VMSettings.DvdDriveInstalled, VirtualizationErrorCodes.QUOTA_EXCEEDED_DVD_ENABLED);
                QuotaHelper.CheckBooleanQuota(cntx, quotaResults, Quotas.VPS2012_BOOT_CD_ALLOWED, VMSettings.BootFromCD, VirtualizationErrorCodes.QUOTA_EXCEEDED_CD_ALLOWED);

                QuotaHelper.CheckBooleanQuota(cntx, quotaResults, Quotas.VPS2012_START_SHUTDOWN_ALLOWED, VMSettings.StartTurnOffAllowed, VirtualizationErrorCodes.QUOTA_EXCEEDED_START_SHUTDOWN_ALLOWED);
                QuotaHelper.CheckBooleanQuota(cntx, quotaResults, Quotas.VPS2012_PAUSE_RESUME_ALLOWED, VMSettings.PauseResumeAllowed, VirtualizationErrorCodes.QUOTA_EXCEEDED_PAUSE_RESUME_ALLOWED);
                QuotaHelper.CheckBooleanQuota(cntx, quotaResults, Quotas.VPS2012_REBOOT_ALLOWED, VMSettings.RebootAllowed, VirtualizationErrorCodes.QUOTA_EXCEEDED_REBOOT_ALLOWED);
                QuotaHelper.CheckBooleanQuota(cntx, quotaResults, Quotas.VPS2012_RESET_ALOWED, VMSettings.ResetAllowed, VirtualizationErrorCodes.QUOTA_EXCEEDED_RESET_ALOWED);
                QuotaHelper.CheckBooleanQuota(cntx, quotaResults, Quotas.VPS2012_REINSTALL_ALLOWED, VMSettings.ReinstallAllowed, VirtualizationErrorCodes.QUOTA_EXCEEDED_REINSTALL_ALLOWED);

                QuotaHelper.CheckBooleanQuota(cntx, quotaResults, Quotas.VPS2012_EXTERNAL_NETWORK_ENABLED, VMSettings.ExternalNetworkEnabled, VirtualizationErrorCodes.QUOTA_EXCEEDED_EXTERNAL_NETWORK_ENABLED);
                QuotaHelper.CheckBooleanQuota(cntx, quotaResults, Quotas.VPS2012_PRIVATE_NETWORK_ENABLED, VMSettings.PrivateNetworkEnabled, VirtualizationErrorCodes.QUOTA_EXCEEDED_PRIVATE_NETWORK_ENABLED);

                // check external addresses number
                if (!randomExternalAddresses && externalAddresses != null)
                    externalAddressesNumber = externalAddresses.Length;

                int maxAddresses = ServerController.GetPackageUnassignedIPAddresses(packageId, IPAddressPool.VpsExternalNetwork).Count; //Get num IPs if they exist
                if (maxAddresses == 0) //get quota for Unallotted IPs network     
                {
                    int max = cntx.Quotas[Quotas.VPS2012_EXTERNAL_IP_ADDRESSES_NUMBER].QuotaAllocatedValue != -1 ?
                        cntx.Quotas[Quotas.VPS2012_EXTERNAL_IP_ADDRESSES_NUMBER].QuotaAllocatedValue : int.MaxValue;
                    maxAddresses = max - cntx.Quotas[Quotas.VPS2012_EXTERNAL_IP_ADDRESSES_NUMBER].QuotaUsedValue;
                }                 
                    

                if (VMSettings.ExternalNetworkEnabled && externalAddressesNumber > maxAddresses)
                    quotaResults.Add(VirtualizationErrorCodes.QUOTA_EXCEEDED_EXTERNAL_ADDRESSES_NUMBER + ":" + maxAddresses.ToString());

                // check private addresses number
                if (!randomPrivateAddresses && privateAddresses != null)
                    privateAddressesNumber = privateAddresses.Length;
                QuotaHelper.CheckNumericQuota(cntx, quotaResults, Quotas.VPS2012_PRIVATE_IP_ADDRESSES_NUMBER, privateAddressesNumber, VirtualizationErrorCodes.QUOTA_EXCEEDED_PRIVATE_ADDRESSES_NUMBER);

                // check management network parameters
                NetworkAdapterDetails manageNic = GetManagementNetworkDetails(packageId);
                if (!String.IsNullOrEmpty(manageNic.NetworkId))
                {
                    // network enabled - check management IPs pool
                    int manageIpsNumber = ServerController.GetUnallottedIPAddresses(
                            packageId, ResourceGroups.VPS2012, IPAddressPool.VpsManagementNetwork).Count;

                    if (manageIpsNumber == 0)
                        quotaResults.Add(VirtualizationErrorCodes.QUOTA_EXCEEDED_MANAGEMENT_NETWORK);
                }

                // check acceptable values
                if (VMSettings.RamSize <= 0)
                    quotaResults.Add(VirtualizationErrorCodes.QUOTA_WRONG_RAM);
                if (VMSettings.HddSize <= 0)
                    quotaResults.Add(VirtualizationErrorCodes.QUOTA_WRONG_HDD);
                if (VMSettings.SnapshotsNumber < 0)
                    quotaResults.Add(VirtualizationErrorCodes.QUOTA_WRONG_SNAPSHOTS);

                if (quotaResults.Count > 0)
                {
                    res.ErrorCodes.AddRange(quotaResults);
                    return res;
                }
                #endregion

                #region Check input parameters
                // check private network IP addresses if they are specified
                List<string> checkResults = CheckPrivateIPAddresses(packageId, privateAddresses);
                if (checkResults.Count > 0)
                {
                    res.ErrorCodes.AddRange(checkResults);
                    return res;
                }
                #endregion

                #region Setup external Unallotted IPs network
                // setup external Unallotted IPs network
                if (VMSettings.ExternalNetworkEnabled)
                {
                    int maxItems = 100000000;
                    PackageIPAddress[] ips = ServerController.GetPackageIPAddresses(packageId, 0,
                                IPAddressPool.VpsExternalNetwork, "", "", "", 0, maxItems, true).Items;
                    if (ips.Length == 0) //if the Customer does not have IP - addresses
                    {
                        // assign selected IP addresses to package
                        ServerController.AllocatePackageIPAddresses(packageId, externalAddresses);

                        // re-read package IPs
                        List<PackageIPAddress> packageIPs = ServerController.GetPackageUnassignedIPAddresses(
                                        packageId, IPAddressPool.VpsExternalNetwork);
                        // get new externalAddresses IDs (Yep, very strange WSP/SolidCP logic)
                        for (int i = 0; i < externalAddresses.Length; i++)
                        {
                            externalAddresses[i] = packageIPs[i].PackageAddressID;
                        }
                    }
                }
                #endregion

                #region Context variables
                // service ID
                int serviceId = GetServiceId(packageId);

                // load service settings
                StringDictionary settings = ServerController.GetServiceSettings(serviceId);
                #endregion

                #region Maintenance Mode Check
                if (IsMaintenanceMode(settings))
                {
                    res.ErrorCodes.Add(VirtualizationErrorCodes.MAINTENANCE_MODE_IS_ENABLE);
                    return res;
                }                
                #endregion

                #region Check host name

                if (string.IsNullOrEmpty(VMSettings.Name))
                {
                    string hostnamePattern = settings["HostnamePattern"];                    
                    if (hostnamePattern.IndexOf("[") == -1) //If we do not find a pattern, replace the string with the default value
                    {
                        hostnamePattern = "ip-[ip_last_4_octects]-id[space_id].hostname.local";
                    }
                    VMSettings.Name = EvaluateSpaceVariables(hostnamePattern, packageId);
                }

                try //TODO: Change this check. It works only in one Package. Just use => packageId = 1?
                {
                    ServiceProviderItem item = PackageController.GetPackageItemByName(packageId, VMSettings.Name,
                                                                                      typeof(VirtualMachine));
                    if (item != null)
                    {
                        res.ErrorCodes.Add(VirtualizationErrorCodes.HOST_NAMER_IS_ALREADY_USED);
                        return res;
                    }
                }
                catch (Exception ex)
                {
                    res.AddError(VirtualizationErrorCodes.CANNOT_CHECK_HOST_EXISTS, ex);
                    return res;
                }

                #endregion

                #region Create meta item
                // create meta item
                vm = VMSettings; //new VirtualMachine();

                //vm.Name = VMSettings.Name;
                vm.AdministratorPassword = CryptoUtils.Encrypt(password);
                vm.PackageId = packageId;
                vm.VirtualMachineId = null; // from service
                vm.ServiceId = serviceId;                
                vm.Version = string.IsNullOrEmpty(settings["HyperVConfigurationVersion"]) ? "0.0" : settings["HyperVConfigurationVersion"];

                vm.CurrentTaskId = Guid.NewGuid().ToString("N"); // generate creation task id
                vm.ProvisioningStatus = VirtualMachineProvisioningStatus.InProgress;

                //vm.Generation = otherSettings.Generation; get from Template
                //vm.CpuCores = cpuCores;
                //vm.RamSize = ramMB;
                //vm.HddSize = hddGB;
                //vm.HddMinimumIOPS = hddMinimumIOPS;
                //vm.HddMaximumIOPS = hddMaximumIOPS;
                //vm.SnapshotsNumber = snapshots;
                //vm.DvdDriveInstalled = dvdInstalled;
                //vm.BootFromCD = bootFromCD;
                //vm.NumLockEnabled = numLock;
                //vm.StartTurnOffAllowed = startShutdownAllowed;
                //vm.PauseResumeAllowed = pauseResumeAllowed;
                //vm.RebootAllowed = rebootAllowed;
                //vm.ResetAllowed = resetAllowed;
                //vm.ReinstallAllowed = reinstallAllowed;
                //vm.defaultaccessvlan = otherSettings.defaultaccessvlan;

                // dynamic memory
                if (VMSettings.DynamicMemory != null && VMSettings.DynamicMemory.Enabled)
                    vm.DynamicMemory = VMSettings.DynamicMemory;
                else
                    vm.DynamicMemory = null;

                // networking
                //vm.ExternalNetworkEnabled = externalNetworkEnabled;
                //vm.PrivateNetworkEnabled = privateNetworkEnabled;
                vm.ManagementNetworkEnabled = !String.IsNullOrEmpty(manageNic.NetworkId);

                // load OS templates
                LibraryItem osTemplate = null;

                try
                {
                    LibraryItem[] osTemplates = GetOperatingSystemTemplates(vm.PackageId);
                    foreach (LibraryItem item in osTemplates)
                    {
                        if (String.Compare(item.Path, osTemplateFile, true) == 0)
                        {
                            osTemplate = item;

                            // check minimal disk size
                            if (osTemplate.DiskSize > 0 && vm.HddSize < osTemplate.DiskSize)
                            {
                                TaskManager.CompleteResultTask(res, VirtualizationErrorCodes.QUOTA_TEMPLATE_DISK_MINIMAL_SIZE + ":" + osTemplate.DiskSize);
                                return res;
                            }
                            if (osTemplate.Generation < 1)
                                throw new Exception("The generation of VM was not configured in the template");
                            vm.Generation = osTemplate.Generation;
                            vm.EnableSecureBoot = osTemplate.Generation == 1 ? false : osTemplate.EnableSecureBoot;
                            vm.OperatingSystemTemplate = osTemplate.Name;
                            vm.LegacyNetworkAdapter = osTemplate.LegacyNetworkAdapter;
                            vm.RemoteDesktopEnabled = osTemplate.RemoteDesktop;
                            break;
                        }
                    }
                }
                catch (Exception ex)
                {
                    res.AddError(VirtualizationErrorCodes.GET_OS_TEMPLATES_ERROR, ex);
                    return res;
                }

                // setup VM paths
                string templatesPath = settings["OsTemplatesPath"];
                string rootFolderPattern = settings["RootFolder"];
                if (rootFolderPattern.IndexOf("[") == -1)
                {
                    // no pattern has been specified
                    if (!rootFolderPattern.EndsWith("\\"))
                        rootFolderPattern += "\\";
                    rootFolderPattern += "[username]\\[vps_hostname]";
                }

                vm.RootFolderPath = EvaluateItemVariables(rootFolderPattern, vm);
                if (!vm.RootFolderPath.EndsWith(vm.Name))  //we must be sure that Path ends with vm.Name (hostname)!
                {
                    vm.RootFolderPath = Path.Combine(vm.RootFolderPath, vm.Name);
                }
                var correctVhdPath = GetCorrectTemplateFilePath(templatesPath, osTemplateFile);
                vm.OperatingSystemTemplatePath = correctVhdPath;
                string msHddHyperVFolderName = "Virtual Hard Disks\\" + vm.Name;
                vm.VirtualHardDrivePath = Path.Combine(vm.RootFolderPath, msHddHyperVFolderName + Path.GetExtension(correctVhdPath));

                // check hdd file
                try
                {
                    VirtualizationServer2012 vs = GetVirtualizationProxy(vm.ServiceId);
                    if (vs.FileExists(vm.VirtualHardDrivePath))
                        throw new Exception(vm.VirtualHardDrivePath + " is already present in the system");
                }
                catch (Exception ex)
                {
                    res.AddError(VirtualizationErrorCodes.HDD_VM_FILE_EXIST_ERROR, ex);
                    return res;
                }

                // save meta-item
                try
                {
                    vm.Id = PackageController.AddPackageItem(vm);
                }
                catch (Exception ex)
                {
                    res.AddError(VirtualizationErrorCodes.CREATE_META_ITEM_ERROR, ex);
                    return res;
                }

                #endregion

                #region Start Asynchronous task
                try
                {
                    // asynchronous process starts here
                    CreateServerAsyncWorker2012 worker = new CreateServerAsyncWorker2012();

                    worker.TaskId = vm.CurrentTaskId; // async task ID
                    worker.ThreadUserId = SecurityContext.User.UserId;
                    worker.Item = vm;
                    worker.OsTemplate = osTemplate;

                    worker.ExternalAddressesNumber = externalAddressesNumber;
                    worker.RandomExternalAddresses = randomExternalAddresses;
                    worker.ExternalAddresses = externalAddresses;

                    worker.PrivateAddressesNumber = privateAddressesNumber;
                    worker.RandomPrivateAddresses = randomPrivateAddresses;
                    worker.PrivateAddresses = privateAddresses;

                    worker.SummaryLetterEmail = summaryLetterEmail;

                    worker.CreateAsync();
                }
                catch (Exception ex)
                {
                    // delete meta item
                    PackageController.DeletePackageItem(vm.Id);

                    // return from method
                    res.AddError(VirtualizationErrorCodes.CREATE_TASK_START_ERROR, ex);
                    return res;
                }
                #endregion
            }
            catch (Exception ex)
            {
                res.AddError(VirtualizationErrorCodes.CREATE_ERROR, ex);
                return res;
            }
            res.Value = vm.Id;
            res.IsSuccess = true;
            return res;
        }

        //[Obsolete("CreateVirtualMachine is deprecated, please use CreateNewVirtualMachine instead.")]
        public static IntResult CreateVirtualMachine(int packageId,
                string hostname, string osTemplateFile, string password, string summaryLetterEmail,
                int cpuCores, int ramMB, int hddGB, int snapshots,
                bool dvdInstalled, bool bootFromCD, bool numLock,
                bool startShutdownAllowed, bool pauseResumeAllowed, bool rebootAllowed, bool resetAllowed, bool reinstallAllowed,
                bool externalNetworkEnabled, int externalAddressesNumber, bool randomExternalAddresses, int[] externalAddresses,
                bool privateNetworkEnabled, int privateAddressesNumber, bool randomPrivateAddresses, string[] privateAddresses, VirtualMachine otherSettings)
        {
            otherSettings.PackageId = packageId;
            otherSettings.Name = hostname;
            otherSettings.CpuCores = cpuCores;
            otherSettings.RamSize = ramMB;
            otherSettings.HddSize = hddGB;
            //otherSettings.HddMinimumIOPS = hddMinimumIOPS;
            //otherSettings.HddMaximumIOPS = hddMaximumIOPS;
            otherSettings.SnapshotsNumber = snapshots;
            otherSettings.DvdDriveInstalled = dvdInstalled;
            otherSettings.BootFromCD = bootFromCD;
            otherSettings.NumLockEnabled = numLock;
            otherSettings.StartTurnOffAllowed = startShutdownAllowed;
            otherSettings.PauseResumeAllowed = pauseResumeAllowed;
            otherSettings.RebootAllowed = rebootAllowed;
            otherSettings.ResetAllowed = resetAllowed;
            otherSettings.ReinstallAllowed = reinstallAllowed;
            otherSettings.ExternalNetworkEnabled = externalNetworkEnabled;
            otherSettings.PrivateNetworkEnabled = privateNetworkEnabled;

            return CreateNewVirtualMachine(otherSettings, osTemplateFile, password, summaryLetterEmail,
                externalAddressesNumber, randomExternalAddresses, externalAddresses,
                privateAddressesNumber, randomPrivateAddresses, privateAddresses);
        }

        private static string GetCorrectTemplateFilePath(string templatesPath, string osTemplateFile)
        {
            return Path.Combine(templatesPath, osTemplateFile);
        }

        internal static void CreateVirtualMachineInternal(string taskId, VirtualMachine vm, LibraryItem osTemplate,
                int externalAddressesNumber, bool randomExternalAddresses, int[] externalAddresses,
                int privateAddressesNumber, bool randomPrivateAddresses, string[] privateAddresses,
                string summaryLetterEmail)
        {
            // start task
            //TaskManager.StartTask(taskId, "VPS2012", "CREATE", vm.Name, vm.Id, vm.PackageId);
            int maximumExecutionSeconds = 60 * 60 * 2; //2 hours for this task. Anyway the Powershell cmd vhd convert has max 1 hour limit.
            TaskManager.StartTask(taskId, "VPS2012", "CREATE", vm.Name, vm.Id, vm.PackageId, maximumExecutionSeconds);

            bool isDiskConverted = false;
            try
            {
                // set Error flag
                vm.ProvisioningStatus = VirtualMachineProvisioningStatus.Error;

                // load proxy
                VirtualizationServer2012 vs = GetVirtualizationProxy(vm.ServiceId);

                // load service settings
                StringDictionary settings = ServerController.GetServiceSettings(vm.ServiceId);

                #region Setup External network
                TaskManager.Write("VPS_CREATE_SETUP_EXTERNAL_NETWORK");
                TaskManager.IndicatorCurrent = -1; // Some providers (for example HyperV2012R2) could not provide progress 

                try
                {
                    if (vm.ExternalNetworkEnabled)
                    {                        
                        // provision IP addresses
                        ResultObject privResult = AddVirtualMachineInternalIPAddresses(vm.Id, randomExternalAddresses,
                            externalAddressesNumber, externalAddresses, false, vm.defaultaccessvlan);

                        // set primary IP address
                        NetworkAdapterDetails extNic = GetExternalNetworkAdapterDetails(vm.Id);
                        if (extNic.IPAddresses.Length > 0)
                            SetVirtualMachinePrimaryExternalIPAddress(vm.Id, extNic.IPAddresses[0].AddressId, false);

                        // connect to network
                        vm.ExternalSwitchId = settings["ExternalNetworkId"];

                        bool generateMAC = true;
                        if (!string.IsNullOrEmpty(vm.ExternalNicMacAddress))
                        {
                            generateMAC = false;
                            vm.ExternalNicMacAddress = vm.ExternalNicMacAddress.Replace(" ", "").Replace(":", "").Replace("-", "");
                            System.Text.RegularExpressions.Regex regex = new System.Text.RegularExpressions.Regex("^[a-fA-F0-9]{12}$");
                            if (!regex.IsMatch(vm.ExternalNicMacAddress))
                                generateMAC = true;
                        }
                        if (generateMAC)
                            vm.ExternalNicMacAddress = GenerateMacAddress();
                    }
                    else
                    {
                        TaskManager.Write("VPS_CREATE_SETUP_EXTERNAL_NETWORK_SKIP");
                    }
                }
                catch (Exception ex)
                {
                    TaskManager.WriteError(ex, "VPS_CREATE_SETUP_EXTERNAL_NETWORK_ERROR");
                    return;
                }
                #endregion

                #region Setup Management network
                TaskManager.Write("VPS_CREATE_SETUP_MANAGEMENT_NETWORK");
                TaskManager.IndicatorCurrent = -1; // Some providers (for example HyperV2012R2) could not provide progress 

                try
                {
                    if (vm.ManagementNetworkEnabled)
                    {
                        // check that package contains unassigned IP
                        // that could be re-used
                        List<PackageIPAddress> packageIps = ServerController.GetPackageUnassignedIPAddresses(
                            vm.PackageId, IPAddressPool.VpsManagementNetwork);

                        if (packageIps.Count == 0)
                        {
                            // must be fresh space
                            // allocate package IP from the pool
                            List<IPAddressInfo> ips = ServerController.GetUnallottedIPAddresses(
                                vm.PackageId, ResourceGroups.VPS2012, IPAddressPool.VpsManagementNetwork);

                            if (ips.Count > 0)
                            {
                                // assign IP to the package
                                ServerController.AllocatePackageIPAddresses(vm.PackageId, new int[] { ips[0].AddressId });

                                // re-read package IPs
                                packageIps = ServerController.GetPackageUnassignedIPAddresses(
                                                vm.PackageId, IPAddressPool.VpsManagementNetwork);
                            }
                            else
                            {
                                // nothing to allocate - pool empty
                                TaskManager.WriteWarning("VPS_CREATE_SETUP_MANAGEMENT_NETWORK_POOL_EMPTY");
                            }
                        }

                        if (packageIps.Count > 0)
                        {
                            // assign to the item
                            ServerController.AddItemIPAddress(vm.Id, packageIps[0].PackageAddressID);

                            // set primary IP address
                            ServerController.SetItemPrimaryIPAddress(vm.Id, packageIps[0].PackageAddressID);

                            // connect to network
                            vm.ManagementSwitchId = settings["ManagementNetworkId"];
                            vm.ManagementNicMacAddress = GenerateMacAddress();
                        }
                    }
                    else
                    {
                        TaskManager.Write("VPS_CREATE_SETUP_MANAGEMENT_NETWORK_SKIP");
                    }
                }
                catch (Exception ex)
                {
                    TaskManager.WriteError(ex, "VPS_CREATE_SETUP_MANAGEMENT_NETWORK_ERROR");
                    return;
                }
                #endregion

                #region Setup Private network
                TaskManager.Write("VPS_CREATE_SETUP_PRIVATE_NETWORK");
                TaskManager.IndicatorCurrent = -1; // Some providers (for example HyperV2012R2) could not provide progress 

                try
                {
                    if (vm.PrivateNetworkEnabled)
                    {
                        NetworkAdapterDetails privNic = GetPrivateNetworkDetailsInternal(vm.ServiceId);

                        if (!privNic.IsDHCP)
                        {
                            // provision IP addresses
                            ResultObject extResult = AddVirtualMachinePrivateIPAddresses(vm.Id, randomPrivateAddresses, privateAddressesNumber, privateAddresses, false);

                            // set primary IP address
                            privNic = GetPrivateNetworkAdapterDetails(vm.Id);
                            if (privNic.IPAddresses.Length > 0)
                                SetVirtualMachinePrimaryPrivateIPAddress(vm.Id, privNic.IPAddresses[0].AddressId, false);
                        }

                        // connecto to network
                        vm.PrivateSwitchId = settings["PrivateNetworkId"];

                        if (String.IsNullOrEmpty(vm.PrivateSwitchId))
                        {
                            // create/load private virtual switch
                            vm.PrivateSwitchId = EnsurePrivateVirtualSwitch(vm);
                            if (vm.PrivateSwitchId == null)
                                return; // exit on error
                        }
                        vm.PrivateNicMacAddress = GenerateMacAddress();
                    }
                    else
                    {
                        TaskManager.Write("VPS_CREATE_SETUP_PRIVATE_NETWORK_SKIP");
                    }
                }
                catch (Exception ex)
                {
                    TaskManager.WriteError(ex, "VPS_CREATE_SETUP_PRIVATE_NETWORK_ERROR");
                    return;
                }
                #endregion

                // update service item
                VirtualMachineProvisioningStatus status = vm.ProvisioningStatus;
                vm.ProvisioningStatus = VirtualMachineProvisioningStatus.InProgress;
                vm.CreationTime = DateTime.Now.ToString();
                PackageController.UpdatePackageItem(vm);
                vm.ProvisioningStatus = status;

                #region Copy/convert VHD
                JobResult result = null;
                ReturnCode code = ReturnCode.OK;

                TaskManager.Write("VPS_CREATE_OS_GENERATION", osTemplate.Generation.ToString());
                if (osTemplate.Generation > 1)
                    TaskManager.Write("VPS_CREATE_OS_SECUREBOOT", osTemplate.EnableSecureBoot ? "Enabled": "Disabled");
                TaskManager.Write("VPS_CREATE_OS_TEMPLATE", osTemplate.Name);                
                TaskManager.Write("VPS_CREATE_CONVERT_VHD");
                if (osTemplate.VhdBlockSizeBytes > 0)
                    TaskManager.Write("VPS_CREATE_CONVERT_SET_VHD_BLOCKSIZE", osTemplate.VhdBlockSizeBytes.ToString());
                TaskManager.Write("VPS_CREATE_CONVERT_SOURCE_VHD", vm.OperatingSystemTemplatePath);
                TaskManager.Write("VPS_CREATE_CONVERT_DEST_VHD", vm.VirtualHardDrivePath);
                TaskManager.IndicatorCurrent = -1;
                try
                {
                    // convert VHD
                    VirtualHardDiskType vhdType = (VirtualHardDiskType)Enum.Parse(typeof(VirtualHardDiskType), settings["VirtualDiskType"], true);
                    result = vs.ConvertVirtualHardDisk(vm.OperatingSystemTemplatePath, vm.VirtualHardDrivePath, vhdType, osTemplate.VhdBlockSizeBytes);

                    // check return
                    if (result.ReturnValue != ReturnCode.JobStarted)
                    {
                        TaskManager.WriteError("VPS_CREATE_CONVERT_VHD_ERROR_JOB_START", result.ReturnValue.ToString());
                        return;
                    }

                    // wait for completion
                    if (!JobCompleted(vs, result.Job))
                    {
                        TaskManager.WriteError("VPS_CREATE_CONVERT_VHD_ERROR_JOB_EXEC", result.Job.ErrorDescription.ToString());
                        return;
                    }

                    isDiskConverted = true; //We are sure that the disc was copied.
                }
                catch (Exception ex)
                {
                    TaskManager.WriteError(ex, "VPS_CREATE_CONVERT_VHD_ERROR");
                    return;
                }                
                #endregion

                #region Get VHD info
                VirtualHardDiskInfo vhdInfo = null;
                try
                {
                    vhdInfo = vs.GetVirtualHardDiskInfo(vm.VirtualHardDrivePath);
                }
                catch (Exception ex)
                {
                    TaskManager.WriteError(ex, "VPS_CREATE_GET_VHD_INFO");
                    return;
                }

                if (vhdInfo == null || vhdInfo.InUse)
                {
                    // master VHD is in use
                    TaskManager.WriteError("VPS_CREATE_MASTER_VHD_IN_USE");
                    return;
                }

                // check if it should be expanded
                int hddSizeGB = Convert.ToInt32(vhdInfo.MaxInternalSize / Size1G);

                TaskManager.Write("VPS_CREATE_EXPAND_SOURCE_VHD_SIZE", hddSizeGB.ToString());
                TaskManager.Write("VPS_CREATE_EXPAND_DEST_VHD_SIZE", vm.HddSize.ToString());
                #endregion

                #region Expand VHD
                bool expanded = false;
                if (vm.HddSize > hddSizeGB)
                {
                    TaskManager.Write("VPS_CREATE_EXPAND_VHD");
                    TaskManager.IndicatorCurrent = -1; // Some providers (for example HyperV2012R2) could not provide progress 

                    // expand VHD
                    try
                    {
                        result = vs.ExpandVirtualHardDisk(vm.VirtualHardDrivePath, (ulong)vm.HddSize);
                    }
                    catch (Exception ex)
                    {
                        TaskManager.WriteError(ex, "VPS_CREATE_EXPAND_VHD_ERROR");
                        return;
                    }

                    // check return
                    if (result.ReturnValue != ReturnCode.JobStarted)
                    {
                        // error starting Expand job
                        TaskManager.WriteError("VPS_CREATE_EXPAND_VHD_ERROR_JOB_START", result.ReturnValue.ToString());
                        return;
                    }

                    // wait for completion
                    if (!JobCompleted(vs, result.Job))
                    {
                        // error executing Expand job
                        TaskManager.WriteError("VPS_CREATE_EXPAND_VHD_ERROR_JOB_EXEC", result.Job.ErrorDescription);
                        return;
                    }
                    expanded = true;
                }
                else
                {
                    // skip expanding
                    TaskManager.Write("VPS_CREATE_EXPAND_VHD_SKIP");
                }
                #endregion

                #region Process VHD contents
                // mount VHD
                if ((expanded && osTemplate.ProcessVolume != -1)
                    || (osTemplate.SysprepFiles != null && osTemplate.SysprepFiles.Length > 0))
                {
                    try
                    {
                        #region Mount VHD
                        byte attemps = 3;
                        MountedDiskInfo mountedInfo = null;

                        while (attemps > 0)
                        {
                            try
                            {
                                //TODO: Is possible to lose vm.VirtualHardDrivePath ? Add Check?
                                mountedInfo = vs.MountVirtualHardDisk(vm.VirtualHardDrivePath);
                                attemps = 0;
                            }
                            catch (Exception ex)
                            {
                                attemps--;
                                if (attemps == 0)
                                    throw ex;

                                Thread.Sleep(5000); //wait and try again.                                
                            }
                        }
                        
                        if (mountedInfo == null)
                        {
                            // mount returned NULL
                            TaskManager.WriteError("VPS_CREATE_MOUNT_VHD_NULL");
                            return;
                        }
                        #endregion

                        #region Expand volume
                        if (expanded && osTemplate.ProcessVolume != -1 && mountedInfo.DiskVolumes.Length > 0)
                        {
                            try
                            {
                                vs.ExpandDiskVolume(mountedInfo.DiskAddress, mountedInfo.DiskVolumes[osTemplate.ProcessVolume]);
                            }
                            catch (Exception ex)
                            {
                                TaskManager.WriteError(ex, "VPS_CREATE_DISKPART_ERROR");
                            }
                        }
                        else
                        {
                            TaskManager.Write("VPS_CREATE_EXPAND_VHD_SKIP_NO_VOLUMES");
                        }
                        #endregion

                        #region Sysprep
                        if (mountedInfo.DiskVolumes.Length > 0
                            && osTemplate.ProcessVolume != -1
                            && osTemplate.SysprepFiles != null && osTemplate.SysprepFiles.Length > 0)
                        {
                            foreach (string remoteFile in osTemplate.SysprepFiles)
                            {
                                try
                                {
                                    TaskManager.Write("VPS_CREATE_SYSPREP_FILE", remoteFile);

                                    // build remote path
                                    string path = remoteFile;
                                    if (!remoteFile.StartsWith("\\"))
                                        path = remoteFile.Substring(remoteFile.IndexOf("\\"));

                                    path = String.Format("{0}:{1}", mountedInfo.DiskVolumes[osTemplate.ProcessVolume], path);

                                    // read remote file
                                    string contents = vs.ReadRemoteFile(path);
                                    if (contents == null)
                                    {
                                        TaskManager.Write("VPS_CREATE_SYSPREP_FILE_NOT_FOUND", remoteFile);
                                        continue;
                                    }

                                    // process file contents
                                    contents = EvaluateVirtualMachineTemplate(vm.Id, false, false, contents);

                                    // write remote file
                                    vs.WriteRemoteFile(path, contents);
                                }
                                catch (Exception ex)
                                {
                                    TaskManager.WriteError("VPS_CREATE_SYSPREP_FILE_ERROR", ex.Message);
                                }
                            }
                        }
                        #endregion

                        #region Unmount VHD
                        try
                        {
                            code = vs.UnmountVirtualHardDisk(vm.VirtualHardDrivePath);
                            if (code != ReturnCode.OK)
                            {
                                TaskManager.WriteError("VPS_CREATE_UNMOUNT_ERROR_JOB_START", code.ToString());
                                return;
                            }
                        }
                        catch (Exception ex)
                        {
                            TaskManager.WriteError(ex, "VPS_CREATE_UNMOUNT_ERROR");
                            return;
                        }
                        #endregion
                    }
                    catch (Exception ex)
                    {
                        // error mounting
                        TaskManager.WriteError(ex, "VPS_CREATE_MOUNT_VHD");
                        return;
                    }
                } // end if (expanded ...
                #endregion

                #region Create Virtual Machine
                TaskManager.Write("VPS_CREATE_CPU_CORES", vm.CpuCores.ToString());
                TaskManager.Write("VPS_CREATE_RAM_SIZE", vm.RamSize.ToString());
                TaskManager.Write("VPS_CREATE_CREATE_VM");
                TaskManager.IndicatorCurrent = -1; // Some providers (for example HyperV2012R2) could not provide progress 
                // create virtual machine
                try
                {
                    // create
                    vm = vs.CreateVirtualMachine(vm);
                }
                catch (Exception ex)
                {
                    TaskManager.WriteError(ex, "VPS_CREATE_CREATE_VM_ERROR");
                    return;
                }

                // update meta item
                PackageController.UpdatePackageItem(vm);

                TaskManager.Write("VPS_CREATE_CREATED_VM");
                #endregion

                // set OK flag
                vm.ProvisioningStatus = VirtualMachineProvisioningStatus.OK;

                #region Send KVP
                // configure computer name
                if (osTemplate.ProvisionComputerName)
                {
                    TaskManager.Write("VPS_CREATE_SET_COMPUTER_NAME_KVP");
                    SendComputerNameKVP(vm.Id, vm.Name);
                }

                // change administrator password
                if (osTemplate.ProvisionAdministratorPassword)
                {
                    TaskManager.Write("VPS_CREATE_SET_PASSWORD_KVP");
                    SendAdministratorPasswordKVP(vm.Id, CryptoUtils.Decrypt(vm.AdministratorPassword), false); //TODO check mb need true
                }

                // configure network adapters
                if(osTemplate.ProvisionNetworkAdapters)
                {
                    // external NIC
                    TaskManager.Write("VPS_CREATE_SET_EXTERNAL_NIC_KVP");
                    if (vm.ExternalNetworkEnabled)
                    {
                        result = SendNetworkAdapterKVP(vm.Id, "External");

                        if (result.ReturnValue != ReturnCode.JobStarted)
                            TaskManager.WriteWarning("VPS_CREATE_SET_EXTERNAL_NIC_KVP_ERROR", result.ReturnValue.ToString());
                    }

                    // management NIC
                    TaskManager.Write("VPS_CREATE_SET_MANAGEMENT_NIC_KVP");
                    if (vm.ManagementNetworkEnabled)
                    {
                        result = SendNetworkAdapterKVP(vm.Id, "Management");

                        if (result.ReturnValue != ReturnCode.JobStarted)
                            TaskManager.WriteWarning("VPS_CREATE_SET_MANAGEMENT_NIC_KVP_ERROR", result.ReturnValue.ToString());
                    }

                    // private NIC
                    TaskManager.Write("VPS_CREATE_SET_PRIVATE_NIC_KVP");
                    if (vm.PrivateNetworkEnabled)
                    {
                        result = SendNetworkAdapterKVP(vm.Id, "Private");

                        if (result.ReturnValue != ReturnCode.JobStarted)
                            TaskManager.WriteWarning("VPS_CREATE_SET_PRIVATE_NIC_KVP_ERROR", result.ReturnValue.ToString());
                    }
                }
                #endregion

                #region Start VPS
                TaskManager.Write("VPS_CREATE_START_VPS");
                TaskManager.IndicatorCurrent = -1; // Some providers (for example HyperV2012R2) could not provide progress 

                try
                {
                    // start virtual machine
                    result = vs.ChangeVirtualMachineState(vm.VirtualMachineId, VirtualMachineRequestedState.Start);

                    // check return
                    if (result.ReturnValue == ReturnCode.JobStarted)
                    {
                        // wait for completion
                        if (!JobCompleted(vs, result.Job))
                        {
                            TaskManager.WriteWarning("VPS_CREATE_START_VPS_ERROR_JOB_EXEC", result.Job.ErrorDescription.ToString());
                        }
                    }
                    else
                    {
                        TaskManager.WriteWarning("VPS_CREATE_START_VPS_ERROR_JOB_START", result.ReturnValue.ToString());
                    }
                }
                catch (Exception ex)
                {
                    TaskManager.WriteWarning("VPS_CREATE_START_VPS_ERROR", ex.Message);
                }
                TaskManager.Write("VPS_CREATE_STARTED_VPS");
                #endregion

                #region Send Summary letter
                // send summary e-mail
                if (!String.IsNullOrEmpty(summaryLetterEmail))
                {
                    SendVirtualMachineSummaryLetter(vm.Id, summaryLetterEmail, null, true);
                }
                #endregion

            }
            catch (Exception ex)
            {
                TaskManager.WriteError(ex, VirtualizationErrorCodes.CREATE_ERROR);
                return;
            }
            finally
            {
                // reset task ID
                vm.CurrentTaskId = null;
                PackageController.UpdatePackageItem(vm);

                if (vm.ProvisioningStatus == VirtualMachineProvisioningStatus.OK)
                    TaskManager.Write("VPS_CREATE_SUCCESS");
                else if (vm.ProvisioningStatus == VirtualMachineProvisioningStatus.Error)
                {
                    TaskManager.Write("VPS_CREATE_ERROR_END");
                    if (isDiskConverted)
                    {
                        //TODO: Add deletion of the broken file. (2019)
                        //// get proxy
                        //VirtualizationServer2012 vs = GetVirtualizationProxy(vm.ServiceId);
                        //if (vs.IsEmptyFolders(vm.RootFolderPath))
                        //{
                        //    vs.DeleteRemoteFile(vm.RootFolderPath);
                        //}
                    }
                }                    

                // complete task
                TaskManager.CompleteTask();
            }
        }

        public static IntResult ImportVirtualMachine(int packageId,
            int serviceId, string vmId,
            string osTemplateFile, string adminPassword,
            bool startShutdownAllowed, bool pauseResumeAllowed, bool rebootAllowed, bool resetAllowed, bool reinstallAllowed,
            string externalNicMacAddress, int[] externalAddresses,
            string managementNicMacAddress, int managementAddress)
        {
            // result object
            IntResult res = new IntResult();

            // meta item
            VirtualMachine item = null;

            try
            {
                #region Check account and space statuses
                // check account
                if (!SecurityContext.CheckAccount(res, DemandAccount.NotDemo | DemandAccount.IsActive | DemandAccount.IsAdmin))
                    return res;

                // check package
                if (!SecurityContext.CheckPackage(res, packageId, DemandPackage.IsActive))
                    return res;

                #endregion

                // load package context
                PackageContext cntx = PackageController.GetPackageContext(packageId);

                item = new VirtualMachine();
                item.ServiceId = serviceId;
                item.PackageId = packageId;
                item.VirtualMachineId = vmId;

                // load service settings
                StringDictionary settings = ServerController.GetServiceSettings(serviceId);

                // load virtual machine info from service
                VirtualizationServer2012 vs = GetVirtualizationProxy(serviceId);
                VirtualMachine vm = vs.GetVirtualMachineEx(vmId);

                // set VM properties
                item.Name = vm.Name;
                item.ProvisioningStatus = VirtualMachineProvisioningStatus.OK;

                item.Generation = vm.Generation;
                item.EnableSecureBoot = vm.EnableSecureBoot;
                item.CpuCores = vm.CpuCores;
                item.RamSize = vm.RamSize;
                //Hyper-V usually loses CreatedDate and set it to 01/01/1601
                item.CreationTime = vm.CreatedDate < DateTime.Now.AddYears(-10) ? DateTime.Now.ToString() : vm.CreatedDate.ToString();
                item.DynamicMemory = vm.DynamicMemory;
                item.HddSize = vm.HddSize;
                item.HddMinimumIOPS = vm.HddMinimumIOPS;
                item.HddMaximumIOPS = vm.HddMaximumIOPS;
                item.VirtualHardDrivePath = vm.VirtualHardDrivePath;
                item.RootFolderPath = Path.GetDirectoryName(vm.VirtualHardDrivePath);
                string msHddHyperVFolderName = "Virtual Hard Disks";
                if (item.RootFolderPath.EndsWith(msHddHyperVFolderName)) //We have to know root folder of VM, not of hdd.
                    item.RootFolderPath = item.RootFolderPath.Substring(0, item.RootFolderPath.Length - msHddHyperVFolderName.Length);
                item.SnapshotsNumber = cntx.Quotas[Quotas.VPS2012_SNAPSHOTS_NUMBER].QuotaAllocatedValue;
                item.DvdDriveInstalled = vm.DvdDriveInstalled;
                item.BootFromCD = vm.BootFromCD;
                item.NumLockEnabled = vm.NumLockEnabled;
                item.StartTurnOffAllowed = startShutdownAllowed;
                item.PauseResumeAllowed = pauseResumeAllowed;
                item.RebootAllowed = rebootAllowed;
                item.ResetAllowed = resetAllowed;
                item.ReinstallAllowed = reinstallAllowed;

                // remote desktop
                if(!String.IsNullOrEmpty(adminPassword))
                {
                    item.RemoteDesktopEnabled = true;
                    item.AdministratorPassword = CryptoUtils.Encrypt(adminPassword);
                }

                // set OS template
                string templatesPath = settings["OsTemplatesPath"];
                var correctVhdPath = GetCorrectTemplateFilePath(templatesPath, osTemplateFile);
                item.OperatingSystemTemplatePath = correctVhdPath;
                try
                {
                    LibraryItem[] osTemplates = GetOperatingSystemTemplatesByServiceId(serviceId);
                    foreach (LibraryItem osTemplate in osTemplates)
                    {
                        if (String.Compare(osTemplate.Path, osTemplateFile, true) == 0)
                        {
                            item.OperatingSystemTemplate = osTemplate.Name;
                            item.LegacyNetworkAdapter = osTemplate.LegacyNetworkAdapter;
                            break;
                        }
                    }
                }
                catch (Exception ex)
                {
                    res.AddError(VirtualizationErrorCodes.GET_OS_TEMPLATES_ERROR, ex);
                    return res;
                }

                // save item
                int itemId = PackageController.AddPackageItem(item);
                item.Id = itemId;
                res.Value = itemId;

                #region Setup external network
                // setup external network
                if (!String.IsNullOrEmpty(externalNicMacAddress))
                {
                    item.ExternalNetworkEnabled = true;
                    item.ExternalNicMacAddress = externalNicMacAddress;
                    item.ExternalSwitchId = settings["ExternalNetworkId"];

                    // assign selected IP addresses to package
                    ServerController.AllocatePackageIPAddresses(packageId, externalAddresses);

                    // re-read package IPs
                    List<PackageIPAddress> packageIPs = ServerController.GetPackageUnassignedIPAddresses(
                                    packageId, IPAddressPool.VpsExternalNetwork);

                    // assign IP addresses to VM
                    for(int i = 0; i < externalAddresses.Length; i++)
                    {
                        foreach (PackageIPAddress ip in packageIPs)
                        {
                            if (ip.AddressID == externalAddresses[i])
                            {
                                // assign to the item
                                ServerController.AddItemIPAddress(itemId, ip.PackageAddressID);

                                // set primary IP address
                                if(i == 0)
                                    ServerController.SetItemPrimaryIPAddress(itemId, ip.PackageAddressID);

                                break;
                            }
                        }
                    }
                }
                #endregion

                #region Setup management network
                // setup management network
                if (!String.IsNullOrEmpty(managementNicMacAddress))
                {
                    item.ManagementNetworkEnabled = true;
                    item.ManagementNicMacAddress = managementNicMacAddress;
                    item.ManagementSwitchId = settings["ManagementNetworkId"];

                    // assign selected IP addresses to package
                    ServerController.AllocatePackageIPAddresses(packageId, new int[] { managementAddress });

                    // re-read package IPs
                    List<PackageIPAddress> packageIPs = ServerController.GetPackageUnassignedIPAddresses(
                                    packageId, IPAddressPool.VpsManagementNetwork);

                    // assign IP addresses to VM
                    foreach (PackageIPAddress ip in packageIPs)
                    {
                        if (ip.AddressID == managementAddress)
                        {
                            // assign to the item
                            ServerController.AddItemIPAddress(itemId, ip.PackageAddressID);

                            break;
                        }
                    }
                }
                #endregion

                // save item once again
                PackageController.UpdatePackageItem(item);
            }
            catch (Exception ex)
            {
                res.AddError(VirtualizationErrorCodes.IMPORT_ERROR, ex);
                return res;
            }

            res.IsSuccess = true;
            return res;
        }

        private static JobResult SendNetworkAdapterKVP(int itemId, string adapterName)
        {
            // load item
            VirtualMachine vm = GetVirtualMachineByItemId(itemId);
            if (vm == null)
                return null;

            // build task parameters
            Dictionary<string, string> props = new Dictionary<string, string>();
            NetworkAdapterDetails nic = null;

            if(String.Compare(adapterName, "external", true) == 0)
            {
                // external
                nic = GetExternalNetworkAdapterDetails(itemId);
            }
            else if(String.Compare(adapterName, "private", true) == 0)
            {
                // private
                nic = GetPrivateNetworkAdapterDetails(itemId);
            }
            else
            {
                // management
                nic = GetManagementNetworkAdapterDetails(itemId);
            }
            
            // network format
            if (nic != null && !String.IsNullOrEmpty(nic.MacAddress))
            {
                props["MAC"] = nic.MacAddress.Replace("-", ":");
                props["EnableDHCP"] = nic.IsDHCP.ToString();
                if (!nic.IsDHCP)
                {
                    string[] ips = new string[nic.IPAddresses.Length];
                    string[] subnetMasks = new string[nic.IPAddresses.Length];
                    for (int i = 0; i < ips.Length; i++)
                    {
                        ips[i] = nic.IPAddresses[i].IPAddress;
                        subnetMasks[i] = nic.IPAddresses[i].SubnetMask;

                        // set gateway from the first (primary) IP
                        if (i == 0)
                            props["DefaultIPGateway"] = nic.IPAddresses[i].DefaultGateway;
                    }

                    props["IPAddress"] = String.Join(";", ips);
                    props["SubnetMask"] = String.Join(";", subnetMasks);

                    // name servers
                    props["PreferredDNSServer"] = nic.PreferredNameServer;
                    if (!String.IsNullOrEmpty(nic.AlternateNameServer))
                        props["PreferredDNSServer"] += ";" + nic.AlternateNameServer;
                }
            }

            // DNS
            if (!props.ContainsKey("PreferredDNSServer")
                || String.IsNullOrEmpty(props["PreferredDNSServer"]))
            {
                props["PreferredDNSServer"] = "0.0.0.0"; // obtain automatically
            }

            // send items
            return SendKvpItems(itemId, "SetupNetworkAdapter", props);
        }

        private static string GetSymbolDelimitedMacAddress(string mac, string delimiter)
        {
            if (String.IsNullOrEmpty(mac))
                return mac;

            StringBuilder sb = new StringBuilder();
            for (int i = 0; i < 6; i++)
            {
                sb.Append(mac[i * 2]).Append(mac[i * 2 + 1]);
                if (i < 5) sb.Append(delimiter);
            }
            return sb.ToString();
        }

        private static JobResult SendComputerNameKVP(int itemId, string computerName)
        {
            // load item
            VirtualMachine vm = GetVirtualMachineByItemId(itemId);
            if (vm == null)
                return null;

            // build task parameters
            Dictionary<string, string> props = new Dictionary<string, string>();

            props["FullComputerName"] = computerName;

            // send items
            return SendKvpItems(itemId, "ChangeComputerName", props);
        }

        private static JobResult SendAdministratorPasswordKVP(int itemId, string password, bool cleanResult)
        {
            // load item
            VirtualMachine vm = GetVirtualMachineByItemId(itemId);
            if (vm == null)
                return null;

            // build task parameters
            Dictionary<string, string> props = new Dictionary<string, string>();

            props["Password"] = password;

            // send items
            if(cleanResult)
                return SendKvpItemsAndCleanResult(itemId, "ChangeAdministratorPassword", props);
            else
                return SendKvpItems(itemId, "ChangeAdministratorPassword", props);
        }

        private static JobResult SendKvpItems(int itemId, string taskName, Dictionary<string, string> taskProps)
        {
            return SendKvpItemsInternal(itemId, taskName, taskProps, false);
        }

        private static JobResult SendKvpItemsAndCleanResult(int itemId, string taskName, Dictionary<string, string> taskProps)
        {
            return SendKvpItemsInternal(itemId, taskName, taskProps, true);
        }

        private static JobResult SendKvpItemsInternal(int itemId, string taskName, Dictionary<string, string> taskProps, bool cleanResult)
        {
            string TASK_PREFIX = "SCP-";
            string TASK_PREFIX_OLD = "WSP-"; //backward compatibility for the WSPanel and the MSPControl version 0000 < 3000 <= ????

            // load item
            VirtualMachine vm = GetVirtualMachineByItemId(itemId);
            if (vm == null)
                return null;

            JobResult result = null;

            // load proxy
            VirtualizationServer2012 vs = GetVirtualizationProxy(vm.ServiceId);

            try
            {
                // delete completed task definitions
                List<string> completedTasks = new List<string>();
                KvpExchangeDataItem[] vmKvps = vs.GetKVPItems(vm.VirtualMachineId);
                foreach (KvpExchangeDataItem vmKvp in vmKvps)
                {
                    if (vmKvp.Name.StartsWith(TASK_PREFIX))
                    {
                        completedTasks.Add(vmKvp.Name);
                        TryToDelUnusedTask(ref vm, ref vs, vmKvp.Name.ToString(), TASK_PREFIX_OLD, TASK_PREFIX);
                    }
                    else if (vmKvp.Name.StartsWith(TASK_PREFIX_OLD))
                    {
                        completedTasks.Add(vmKvp.Name);
                        TryToDelUnusedTask(ref vm, ref vs, vmKvp.Name.ToString(), TASK_PREFIX, TASK_PREFIX_OLD);
                    }
                }

                // delete completed items
                vs.RemoveKVPItems(vm.VirtualMachineId, completedTasks.ToArray());
            }
            catch (Exception ex)
            {
                // log error
                TaskManager.WriteWarning(String.Format("Error deleting KVP items: {0}", ex.Message));
            }

            // build items array
            List<string> items = new List<string>();
            foreach (string propName in taskProps.Keys)
                items.Add(propName + "=" + taskProps[propName]);

            //taskName = String.Format("{0}{1}-{2}", TASK_PREFIX, taskName, DateTime.Now.Ticks);
            //string taskData = String.Join("|", items.ToArray());
            string[] taskNameArr = new string[2];
            long dataNowTick = DateTime.Now.Ticks;
            taskNameArr[0] = String.Format("{0}{1}-{2}", TASK_PREFIX, taskName, dataNowTick);
            taskNameArr[1] = String.Format("{0}{1}-{2}", TASK_PREFIX_OLD, taskName, dataNowTick);
            string taskData = String.Join("|", items.ToArray());

            // create KVP item
            KvpExchangeDataItem[] kvp = new KvpExchangeDataItem[taskNameArr.Length];
            for (int i = 0; i < kvp.Length; i++)
            {
                kvp[i] = new KvpExchangeDataItem();
                kvp[i].Name = taskNameArr[i];
                kvp[i].Data = taskData;
            }

            try
            {
                // try adding KVP items
                result = vs.AddKVPItems(vm.VirtualMachineId, kvp);
                TaskManager.Write(String.Format("Trying to add the Task"));
                if (result.Job != null && result.Job.JobState == ConcreteJobState.Exception)
                {
                    // try updating KVP items
                    TaskManager.Write(String.Format("Trying to update the task in the VPS"));
                    return vs.ModifyKVPItems(vm.VirtualMachineId, kvp);
                }
                else
                {
                    TaskManager.Write(String.Format("The task has been sent to the VPS"));
                    if (cleanResult)
                    {
                        Thread t = new Thread(() => CleanLastKVPResult(ref vm, ref vs, TASK_PREFIX, TASK_PREFIX_OLD, taskNameArr));
                        t.Start();
                        //CleanLastKVPResult(ref vm, ref vs, TASK_PREFIX, TASK_PREFIX_OLD, taskNameArr);
                    }
                    return result;
                }
            }
            catch (Exception ex)
            {
                // log error
                TaskManager.WriteWarning(String.Format("Error setting KVP items '{0}': {1}", kvp[0].Data, ex.Message));
            }

            return null;
        }

        private static void CleanLastKVPResult(ref VirtualMachine vm, ref VirtualizationServer2012 vs, string TASK_PREFIX, string TASK_PREFIX_OLD, string[] taskNameArr)
        {
            try
            {
                ushort waitSec = 60;
                for(ushort i = 0; i < waitSec; i++)
                {
                    KvpExchangeDataItem[] vmKvps = vs.GetKVPItems(vm.VirtualMachineId);
                    System.Threading.Thread.Sleep(1000);
                    foreach (KvpExchangeDataItem vmKvp in vmKvps)
                    {
                        if (vmKvp.Name.Equals(taskNameArr[0]))
                        {
                            TryToDelUnusedTask(ref vm, ref vs, vmKvp.Name.ToString(), TASK_PREFIX_OLD, TASK_PREFIX);                            
                            vs.RemoveKVPItems(vm.VirtualMachineId, new string[] { taskNameArr[0] });
                            i = waitSec;
                        }
                        else if (vmKvp.Name.Equals(taskNameArr[1]))
                        {
                            TryToDelUnusedTask(ref vm, ref vs, vmKvp.Name.ToString(), TASK_PREFIX, TASK_PREFIX_OLD);                            
                            vs.RemoveKVPItems(vm.VirtualMachineId, new string[] { taskNameArr[1] });
                            i = waitSec;
                        }
                    }                    
                }
            }
            catch (Exception ex)
            {
                // log error
                TaskManager.WriteWarning(String.Format("Error clean last KVP item: {0}", ex.Message));
            }
        }

        private static void TryToDelUnusedTask(ref VirtualMachine vm, ref VirtualizationServer2012 vs, string taskName, string PREFIX, string REPLACE_PREFIX)
        {
            if (taskName.Substring(PREFIX.Length).Equals("CurrentTask")) //Ignore CurrentTask
            {
                return;
            }

            try
            {
                taskName = taskName.Replace(REPLACE_PREFIX, PREFIX);
                vs.RemoveKVPItems(vm.VirtualMachineId, new string[] { taskName });
            }
            catch (Exception ex)
            {
                // log error
                TaskManager.WriteWarning(String.Format("Error deleting  Unused KVP items: {0}", ex.Message));
            }
        }

        private static string EnsurePrivateVirtualSwitch(ServiceProviderItem item)
        {
            // try locate switch in the package
            List<ServiceProviderItem> items = PackageController.GetPackageItemsByType(item.PackageId, typeof(VirtualSwitch));

            // exists - return ID
            if (items.Count > 0)
                return ((VirtualSwitch)items[0]).SwitchId;

            // switch name
            string name = EvaluateItemVariables("[username] - [space_name]", item);

            // log
            TaskManager.Write("VPS_CREATE_PRIVATE_VIRTUAL_SWITCH", name);

            try
            {
                // create switch
                // load proxy
                VirtualizationServer2012 vs = GetVirtualizationProxy(item.ServiceId);

                // create switch
                VirtualSwitch sw = vs.CreateSwitch(name);
                sw.ServiceId = item.ServiceId;
                sw.PackageId = item.PackageId;

                // save item
                PackageController.AddPackageItem(sw);

                return sw.SwitchId;
            }
            catch (Exception ex)
            {
                TaskManager.WriteError(ex, "VPS_CREATE_PRIVATE_VIRTUAL_SWITCH_ERROR");
                return null;
            }
        }

        private static string EvaluateItemVariables(string str, ServiceProviderItem item)
        {
            str = Utils.ReplaceStringVariable(str, "vps_hostname", item.Name);

            return EvaluateSpaceVariables(str, item.PackageId);
        }

        private static string EvaluateSpaceVariables(string str, int packageId)
        {
            // load package
            PackageInfo package = PackageController.GetPackage(packageId);
            UserInfo user = UserController.GetUser(package.UserId);
            // get 1 IP of VM
            PackageIPAddress[] ips = ServerController.GetPackageIPAddresses(packageId, 0,
                                IPAddressPool.VpsExternalNetwork, "", "", "", 0, 1, true).Items;

            str = Utils.ReplaceStringVariable(str, "space_id", packageId.ToString());
            str = Utils.ReplaceStringVariable(str, "space_name", package.PackageName);
            str = Utils.ReplaceStringVariable(str, "user_id", user.UserId.ToString());
            str = Utils.ReplaceStringVariable(str, "username", user.Username);
            str = Utils.ReplaceStringVariable(str, "ip_last_1_octect", GetIPv4LastOctetsFromPackage(1,packageId,ips));
            str = Utils.ReplaceStringVariable(str, "ip_last_2_octects", GetIPv4LastOctetsFromPackage(2, packageId, ips));
            str = Utils.ReplaceStringVariable(str, "ip_last_3_octects", GetIPv4LastOctetsFromPackage(3, packageId, ips));
            str = Utils.ReplaceStringVariable(str, "ip_last_4_octects", GetIPv4LastOctetsFromPackage(4, packageId, ips));

            return EvaluateRandomSymbolsVariables(str);
        }
                
        private static string EvaluateRandomSymbolsVariables(string str)
        {
            str = Utils.ReplaceStringVariable(str, "guid", Guid.NewGuid().ToString("N"));
            str = Utils.ReplaceStringVariable(str, "mac", GenerateMacAddress());

            return str;
        }

        private static string GetIPv4LastOctetsFromPackage(ushort octets, int packageId)
        {
            return GetIPv4LastOctetsFromPackage(octets, packageId, null);
        }
        private static string GetIPv4LastOctetsFromPackage(ushort octets, int packageId, PackageIPAddress[] ips)
        {
            int maxItems = 1;
            string ExternalIP = "127.0.0.1"; //just a default IP
            if(ips == null || ips.Length == 0)
                ips = ServerController.GetPackageIPAddresses(packageId, 0,
                                IPAddressPool.VpsExternalNetwork, "", "", "", 0, maxItems, true).Items;
            if(ips.Length > 0)
                ExternalIP = ips[0].ExternalIP;            

            byte[] Bytes = System.Net.IPAddress.Parse(ExternalIP).GetAddressBytes();
            StringBuilder sb = new StringBuilder();
            for (int i = 4 - octets; i < 4; i++)
                sb.AppendFormat("{0}-", Bytes[i]);
            sb.Length--; //delete the last symbol "-"

            return sb.ToString();
        } 
        #endregion

        #region VPS – General

        public static List<ConcreteJob> GetVirtualMachineJobs(int itemId)
        {
            // load meta item
            VirtualMachine vm = GetVirtualMachineByItemId(itemId);

            if (vm == null)
                return null;

            // get proxy
            VirtualizationServer2012 vps = GetVirtualizationProxy(vm.ServiceId);

            // load jobs
            ConcreteJob[] jobs = vps.GetVirtualMachineJobs(vm.VirtualMachineId);
            List<ConcreteJob> retJobs = new List<ConcreteJob>();

            foreach (ConcreteJob job in jobs)
            {
                if (job.JobState == ConcreteJobState.Running)
                {
                    retJobs.Add(job);
                }
            }

            return retJobs;
        }
        
        public static byte[] GetVirtualMachineThumbnail(int itemId, ThumbnailSize size)
        {
            // load meta item
            VirtualMachine vm = GetVirtualMachineByItemId(itemId);

            if (vm == null)
                return null;

            // get proxy
            VirtualizationServer2012 vps = GetVirtualizationProxy(vm.ServiceId);

            // return thumbnail
            return vps.GetVirtualMachineThumbnailImage(vm.VirtualMachineId, size);
        }

        public static VirtualMachine GetVirtualMachineGeneralDetails(int itemId)
        {
            // load meta item
            VirtualMachine machine = GetVirtualMachineByItemId(itemId);

            if (machine == null || String.IsNullOrEmpty(machine.VirtualMachineId))
                return null;

            // get proxy
            VirtualizationServer2012 vps = GetVirtualizationProxy(machine.ServiceId);

            // load details
            VirtualMachine vm = vps.GetVirtualMachine(machine.VirtualMachineId);

            // add meta props
            vm.Id = machine.Id;
            vm.Name = machine.Name;
            vm.RamSize = machine.RamSize;
            vm.ServiceId = machine.ServiceId;
            vm.CreationTime = machine.CreationTime;
            vm.ExternalNicMacAddress = machine.ExternalNicMacAddress;

            return vm;
        }

        public static VirtualMachine GetVirtualMachineExtendedInfo(int serviceId, string vmId)
        {
            // get proxy
            VirtualizationServer2012 vps = GetVirtualizationProxy(serviceId);

            // load details
            return vps.GetVirtualMachineEx(vmId);
        }

        public static int CancelVirtualMachineJob(string jobId)
        {
            // VPS - CANCEL_JOB
            return 0;
        }

        public static ResultObject UpdateVirtualMachineHostName(int itemId, string hostname, bool updateNetBIOS)
        {
            if (String.IsNullOrEmpty(hostname))
                throw new ArgumentNullException("hostname");

            ResultObject res = new ResultObject();

            // load service item
            VirtualMachine vm = (VirtualMachine)PackageController.GetPackageItem(itemId);
            if (vm == null)
            {
                res.ErrorCodes.Add(VirtualizationErrorCodes.CANNOT_FIND_VIRTUAL_MACHINE_META_ITEM);
                return res;
            }

            #region Check account and space statuses
            // check account
            if (!SecurityContext.CheckAccount(res, DemandAccount.NotDemo | DemandAccount.IsActive))
                return res;

            // check package
            if (!SecurityContext.CheckPackage(res, vm.PackageId, DemandPackage.IsActive))
                return res;
            #endregion

            // start task
            res = TaskManager.StartResultTask<ResultObject>("VPS", "UPDATE_HOSTNAME", vm.Id, vm.Name, vm.PackageId);

            try
            {
                // get proxy
                VirtualizationServer2012 vs = GetVirtualizationProxy(vm.ServiceId);

                // update virtual machine name
                JobResult result = vs.RenameVirtualMachine(vm.VirtualMachineId, hostname);
                if (result.ReturnValue != ReturnCode.OK)
                {
                    LogReturnValueResult(res, result);
                    TaskManager.CompleteResultTask(res);
                    return res;
                }

                // update meta item
                vm.Name = hostname;
                PackageController.UpdatePackageItem(vm);
                
                // update NetBIOS name if required
                if (updateNetBIOS)
                {
                    result = SendComputerNameKVP(itemId, hostname);
                    if (result.ReturnValue != ReturnCode.JobStarted
                        && result.Job.JobState == ConcreteJobState.Completed)
                    {
                        LogReturnValueResult(res, result);
                        TaskManager.CompleteResultTask(res);
                        return res;
                    }
                }
            }
            catch (Exception ex)
            {
                TaskManager.CompleteResultTask(res, VirtualizationErrorCodes.CHANGE_ADMIN_PASSWORD_ERROR, ex);
                return res;
            }

            TaskManager.CompleteResultTask();
            return res;
        }

        public static ResultObject ChangeVirtualMachineStateExternal(int itemId, VirtualMachineRequestedState state)
        {
            ResultObject res = new ResultObject();

            // load service item
            VirtualMachine vm = (VirtualMachine)PackageController.GetPackageItem(itemId);
            if (vm == null)
            {
                res.ErrorCodes.Add(VirtualizationErrorCodes.CANNOT_FIND_VIRTUAL_MACHINE_META_ITEM);
                return res;
            }

            #region Check account and space statuses
            // check account
            if (!SecurityContext.CheckAccount(res, DemandAccount.NotDemo | DemandAccount.IsActive))
                return res;

            // check package
            if (!SecurityContext.CheckPackage(res, vm.PackageId, DemandPackage.IsActive))
                return res;
            #endregion

            #region Check Quotas
            // check quotas
            List<string> quotaResults = new List<string>();

            if ((state == VirtualMachineRequestedState.Start
                || state == VirtualMachineRequestedState.TurnOff
                || state == VirtualMachineRequestedState.ShutDown)
                && !vm.StartTurnOffAllowed)
                quotaResults.Add(VirtualizationErrorCodes.QUOTA_EXCEEDED_START_SHUTDOWN_ALLOWED);

            else if ((state == VirtualMachineRequestedState.Pause
                || state == VirtualMachineRequestedState.Resume)
                && !vm.PauseResumeAllowed)
                quotaResults.Add(VirtualizationErrorCodes.QUOTA_EXCEEDED_PAUSE_RESUME_ALLOWED);

            else if (state == VirtualMachineRequestedState.Reboot
                && !vm.RebootAllowed)
                quotaResults.Add(VirtualizationErrorCodes.QUOTA_EXCEEDED_REBOOT_ALLOWED);

            else if (state == VirtualMachineRequestedState.Reset
                && !vm.ResetAllowed)
                quotaResults.Add(VirtualizationErrorCodes.QUOTA_EXCEEDED_RESET_ALOWED);

            if (quotaResults.Count > 0)
            {
                res.ErrorCodes.AddRange(quotaResults);
                res.IsSuccess = false;
                TaskManager.CompleteResultTask();
                return res;
            }
            #endregion

            return ChangeVirtualMachineState(itemId, state);
        }

        private static ResultObject ChangeVirtualMachineState(int itemId, VirtualMachineRequestedState state)
        {
            // start task
            ResultObject res = TaskManager.StartResultTask<ResultObject>("VPS", "CHANGE_STATE");
                                                
            try
            {
                // load service item
                VirtualMachine machine = (VirtualMachine)PackageController.GetPackageItem(itemId);
                if (machine == null)
                {
                    TaskManager.CompleteResultTask(res, VirtualizationErrorCodes.CANNOT_FIND_VIRTUAL_MACHINE_META_ITEM);
                    return res;
                }

                BackgroundTask topTask = TaskManager.TopTask;
                topTask.ItemId = machine.Id;
                topTask.ItemName = machine.Name;
                topTask.PackageId = machine.PackageId;

                TaskController.UpdateTask(topTask);

                TaskManager.WriteParameter("New state", state);

                // load proxy
                VirtualizationServer2012 vps = GetVirtualizationProxy(machine.ServiceId);                    

                try
                {
                    if (state == VirtualMachineRequestedState.ShutDown)
                    {
                        ReturnCode code = vps.ShutDownVirtualMachine(machine.VirtualMachineId, true, SHUTDOWN_REASON);
                        if (code != ReturnCode.OK)
                        {
                            res.ErrorCodes.Add(VirtualizationErrorCodes.JOB_START_ERROR + ":" + code);
                            TaskManager.CompleteResultTask(res);
                            return res;
                        }

                        // spin until fully stopped
                        VirtualMachine vm = vps.GetVirtualMachine(machine.VirtualMachineId);
                        short timeOut = 600; //10 min
                        while (vm.State != VirtualMachineState.Off)
                        {
                            timeOut--;
                            System.Threading.Thread.Sleep(1000); // sleep 1 second
                            vm = vps.GetVirtualMachine(machine.VirtualMachineId);
                            if(timeOut == 0)// turnoff
                            {                                
                                ResultObject turnOffResult = ChangeVirtualMachineState(itemId,
                                                                VirtualMachineRequestedState.TurnOff);
                                if (!turnOffResult.IsSuccess)
                                {
                                    TaskManager.CompleteResultTask(res);
                                    return turnOffResult;
                                }
                            }                                
                        }
                    }
                    else if (state == VirtualMachineRequestedState.Reboot)
                    {
                        // shutdown first
                        ResultObject shutdownResult = ChangeVirtualMachineState(itemId,
                            VirtualMachineRequestedState.ShutDown);
                        if (!shutdownResult.IsSuccess)
                        {
                            TaskManager.CompleteResultTask(res);
                            return shutdownResult;
                        }

                        // start machine
                        ResultObject startResult = ChangeVirtualMachineState(itemId, VirtualMachineRequestedState.Start);
                        if (!startResult.IsSuccess)
                        {
                            TaskManager.CompleteResultTask(res);
                            return startResult;
                        }
                    }
                    else if (state == VirtualMachineRequestedState.Reset)
                    {
                        // reset machine
                        JobResult result = vps.ChangeVirtualMachineState(machine.VirtualMachineId, VirtualMachineRequestedState.Reset);

                        if (result.Job.JobState == ConcreteJobState.Completed)
                            {
                                LogReturnValueResult(res, result);
                                TaskManager.CompleteTask();
                                return res;
                            }
                            else
                            {
                                // check return
                                if (result.ReturnValue != ReturnCode.JobStarted)
                                {
                                    LogReturnValueResult(res, result);
                                    TaskManager.CompleteResultTask(res);
                                    return res;
                                }

                                // wait for completion
                                if (!JobCompleted(vps, result.Job))
                                {
                                    LogJobResult(res, result.Job);
                                    TaskManager.CompleteResultTask(res);
                                    return res;
                                }
                            }
                    }
                    else
                    {
                        if (state == VirtualMachineRequestedState.Resume)
                            state = VirtualMachineRequestedState.Start;

                        JobResult result = vps.ChangeVirtualMachineState(machine.VirtualMachineId, state);

                        if (result.Job.JobState == ConcreteJobState.Completed)
                        {
                            LogReturnValueResult(res, result);
                            TaskManager.CompleteTask();
                            return res;
                        }
                        else
                        {
                            // check return
                            if (result.ReturnValue != ReturnCode.JobStarted)
                            {
                                LogReturnValueResult(res, result);
                                TaskManager.CompleteResultTask(res);
                                return res;
                            }

                            // wait for completion
                            if (!JobCompleted(vps, result.Job))
                            {
                                LogJobResult(res, result.Job);
                                TaskManager.CompleteResultTask(res);
                                return res;
                            }
                        }
                    }
                }
                catch(Exception ex)
                {
                    res.IsSuccess = false;
                    res.ErrorCodes.Add(VirtualizationErrorCodes.CANNOT_CHANGE_VIRTUAL_SERVER_STATE);
                    TaskManager.WriteError(ex);
                }
            }
            catch (Exception ex)
            {
                res.IsSuccess = false;
                res.ErrorCodes.Add(VirtualizationErrorCodes.CHANGE_VIRTUAL_MACHINE_STATE_GENERAL_ERROR);
                TaskManager.WriteError(ex);
                return res;
            }

            TaskManager.CompleteTask();
            return res;
        }
        #endregion

        #region VPS - Configuration
        public static ResultObject ChangeAdministratorPassword(int itemId, string password)
        {
            return ChangeAdministratorPasswordInternal(itemId, password, false);
        }

        public static ResultObject ChangeAdministratorPasswordAndCleanResult(int itemId, string password)
        {
            return ChangeAdministratorPasswordInternal(itemId, password, true);
        }

        protected static ResultObject ChangeAdministratorPasswordInternal(int itemId, string password, bool cleanResult)
        {
            ResultObject res = new ResultObject();

            // load service item
            VirtualMachine vm = (VirtualMachine)PackageController.GetPackageItem(itemId);
            if (vm == null)
            {
                res.ErrorCodes.Add(VirtualizationErrorCodes.CANNOT_FIND_VIRTUAL_MACHINE_META_ITEM);
                return res;
            }

            #region Check account and space statuses
            // check account
            if (!SecurityContext.CheckAccount(res, DemandAccount.NotDemo | DemandAccount.IsActive))
                return res;

            // check package
            if (!SecurityContext.CheckPackage(res, vm.PackageId, DemandPackage.IsActive))
                return res;
            #endregion

            // start task
            res = TaskManager.StartResultTask<ResultObject>("VPS", "CHANGE_ADMIN_PASSWORD", vm.Id, vm.Name, vm.PackageId);

            try
            {
                // get proxy
                VirtualizationServer2012 vs = GetVirtualizationProxy(vm.ServiceId);

                // change administrator password
                JobResult result = SendAdministratorPasswordKVP(itemId, password, cleanResult);
                if (result.ReturnValue != ReturnCode.JobStarted
                    && result.Job.JobState == ConcreteJobState.Completed)
                {
                    LogReturnValueResult(res, result);
                    TaskManager.CompleteResultTask(res);
                    return res;
                }

                // update meta item
                vm.AdministratorPassword = CryptoUtils.Encrypt(password);
                PackageController.UpdatePackageItem(vm);
            }
            catch (Exception ex)
            {
                TaskManager.CompleteResultTask(res, VirtualizationErrorCodes.CHANGE_ADMIN_PASSWORD_ERROR, ex);
                return res;
            }

            TaskManager.CompleteResultTask();
            return res;
        }
        #endregion

        #region VPS – Edit Configuration
        public static ResultObject UpdateVirtualMachineResource(int itemId, VirtualMachine vmSettings) 
        {
            ResultObject res = new ResultObject();

            // load service item
            VirtualMachine vm = (VirtualMachine)PackageController.GetPackageItem(itemId);
            if (vm == null)
            {
                res.ErrorCodes.Add(VirtualizationErrorCodes.CANNOT_FIND_VIRTUAL_MACHINE_META_ITEM);
                return res;
            }

            #region Check account and space statuses
            // check account
            if (!SecurityContext.CheckAccount(res, DemandAccount.NotDemo | DemandAccount.IsActive))
                return res;

            // check package
            if (!SecurityContext.CheckPackage(res, vm.PackageId, DemandPackage.IsActive))
                return res;
            #endregion

            #region Check Quotas
            // check quotas
            List<string> quotaResults = new List<string>();
            PackageContext cntx = PackageController.GetPackageContext(vm.PackageId);

            var currentRam = vm.RamSize;
            var newRam = vmSettings.RamSize;

            // dynamic memory
            if (vm.DynamicMemory != null && vm.DynamicMemory.Enabled)
                currentRam = vm.DynamicMemory.Maximum;
            if (vmSettings.DynamicMemory != null && vmSettings.DynamicMemory.Enabled)
            {
                newRam = vmSettings.DynamicMemory.Maximum;

                if (vmSettings.RamSize > vmSettings.DynamicMemory.Maximum || vmSettings.RamSize < vmSettings.DynamicMemory.Minimum)
                    quotaResults.Add(VirtualizationErrorCodes.QUOTA_NOT_IN_DYNAMIC_RAM);
            }

            QuotaHelper.CheckNumericQuota(cntx, quotaResults, Quotas.VPS2012_CPU_NUMBER, vm.CpuCores, vmSettings.CpuCores, VirtualizationErrorCodes.QUOTA_EXCEEDED_CPU);
            QuotaHelper.CheckNumericQuota(cntx, quotaResults, Quotas.VPS2012_RAM, currentRam, newRam, VirtualizationErrorCodes.QUOTA_EXCEEDED_RAM);
            QuotaHelper.CheckNumericQuota(cntx, quotaResults, Quotas.VPS2012_HDD, vm.HddSize, vmSettings.HddSize, VirtualizationErrorCodes.QUOTA_EXCEEDED_HDD);
            QuotaHelper.CheckNumericQuota(cntx, quotaResults, Quotas.VPS2012_SNAPSHOTS_NUMBER, vmSettings.SnapshotsNumber, VirtualizationErrorCodes.QUOTA_EXCEEDED_SNAPSHOTS);

            QuotaHelper.CheckBooleanQuota(cntx, quotaResults, Quotas.VPS2012_DVD_ENABLED, vmSettings.DvdDriveInstalled, VirtualizationErrorCodes.QUOTA_EXCEEDED_DVD_ENABLED);
            QuotaHelper.CheckBooleanQuota(cntx, quotaResults, Quotas.VPS2012_BOOT_CD_ALLOWED, vmSettings.BootFromCD, VirtualizationErrorCodes.QUOTA_EXCEEDED_CD_ALLOWED);

            QuotaHelper.CheckBooleanQuota(cntx, quotaResults, Quotas.VPS2012_START_SHUTDOWN_ALLOWED, vmSettings.StartTurnOffAllowed, VirtualizationErrorCodes.QUOTA_EXCEEDED_START_SHUTDOWN_ALLOWED);
            QuotaHelper.CheckBooleanQuota(cntx, quotaResults, Quotas.VPS2012_PAUSE_RESUME_ALLOWED, vmSettings.PauseResumeAllowed, VirtualizationErrorCodes.QUOTA_EXCEEDED_PAUSE_RESUME_ALLOWED);
            QuotaHelper.CheckBooleanQuota(cntx, quotaResults, Quotas.VPS2012_REBOOT_ALLOWED, vmSettings.RebootAllowed, VirtualizationErrorCodes.QUOTA_EXCEEDED_REBOOT_ALLOWED);
            QuotaHelper.CheckBooleanQuota(cntx, quotaResults, Quotas.VPS2012_RESET_ALOWED, vmSettings.ResetAllowed, VirtualizationErrorCodes.QUOTA_EXCEEDED_RESET_ALOWED);
            QuotaHelper.CheckBooleanQuota(cntx, quotaResults, Quotas.VPS2012_REINSTALL_ALLOWED, vmSettings.ReinstallAllowed, VirtualizationErrorCodes.QUOTA_EXCEEDED_REINSTALL_ALLOWED);

            QuotaHelper.CheckBooleanQuota(cntx, quotaResults, Quotas.VPS2012_EXTERNAL_NETWORK_ENABLED, vmSettings.ExternalNetworkEnabled, VirtualizationErrorCodes.QUOTA_EXCEEDED_EXTERNAL_NETWORK_ENABLED);
            QuotaHelper.CheckBooleanQuota(cntx, quotaResults, Quotas.VPS2012_PRIVATE_NETWORK_ENABLED, vmSettings.PrivateNetworkEnabled, VirtualizationErrorCodes.QUOTA_EXCEEDED_PRIVATE_NETWORK_ENABLED);

            // check acceptable values
            if (vmSettings.RamSize <= 0)
                quotaResults.Add(VirtualizationErrorCodes.QUOTA_WRONG_RAM);
            if (vmSettings.HddSize <= 0)
                quotaResults.Add(VirtualizationErrorCodes.QUOTA_WRONG_HDD);
            if (vmSettings.SnapshotsNumber < 0)
                quotaResults.Add(VirtualizationErrorCodes.QUOTA_WRONG_SNAPSHOTS);

            // IOPS checks
            //TODO

            if (quotaResults.Count > 0)
            {
                res.ErrorCodes.AddRange(quotaResults);
                return res;
            }
            #endregion

            // start task
            res = TaskManager.StartResultTask<ResultObject>("VPS", "UPDATE_CONFIGURATION", vm.Id, vm.Name, vm.PackageId);

            try
            {
                JobResult result = null;

                // get proxy
                VirtualizationServer2012 vs = GetVirtualizationProxy(vm.ServiceId);

                // stop VPS if required
                VirtualMachine vps = vs.GetVirtualMachine(vm.VirtualMachineId);

                bool wasStarted = false;

                // stop (shut down) virtual machine
                if (vps.State != VirtualMachineState.Off)
                {
                    wasStarted = true;
                    ReturnCode code = vs.ShutDownVirtualMachine(vm.VirtualMachineId, true, SHUTDOWN_REASON_CHANGE_CONFIG);
                    if (code == ReturnCode.OK)
                    {
                        // spin until fully stopped
                        vps = vs.GetVirtualMachine(vm.VirtualMachineId);
                        short timeOut = 60 * 10; //10 min
                        while (vps.State != VirtualMachineState.Off) //TODO: rewrite
                        {
                            timeOut--;
                            System.Threading.Thread.Sleep(1000); // sleep 1 second
                            vps = vs.GetVirtualMachine(vm.VirtualMachineId);
                            if (timeOut == 0)// turnoff
                            {
                                ResultObject turnOffResult = ChangeVirtualMachineState(itemId,
                                                                VirtualMachineRequestedState.TurnOff);
                                if (!turnOffResult.IsSuccess)
                                {
                                    TaskManager.CompleteResultTask(res);
                                    return turnOffResult;
                                }
                            }
                        }
                    }
                    else
                    {
                        // turn off
                        result = vs.ChangeVirtualMachineState(vm.VirtualMachineId, VirtualMachineRequestedState.TurnOff);
                        if (!JobCompleted(vs, result.Job))
                        {
                            LogJobResult(res, result.Job);
                            TaskManager.CompleteResultTask(res);
                            return res;
                        }
                    }
                } // end OFF

                /////////////////////////////////////////////
                // update meta-item //TODO: rewrite 
                //vm = vmSettings; //heh we can't do that :(
                vm.CpuCores = vmSettings.CpuCores;
                vm.RamSize = vmSettings.RamSize;
                vm.HddSize = vmSettings.HddSize;
                vm.HddMinimumIOPS = vmSettings.HddMinimumIOPS;
                vm.HddMaximumIOPS = vmSettings.HddMaximumIOPS;
                vm.SnapshotsNumber = vmSettings.SnapshotsNumber;

                vm.BootFromCD = vmSettings.BootFromCD;
                vm.NumLockEnabled = vmSettings.NumLockEnabled;
                vm.DvdDriveInstalled = vmSettings.DvdDriveInstalled;

                vm.StartTurnOffAllowed = vmSettings.StartTurnOffAllowed;
                vm.PauseResumeAllowed = vmSettings.PauseResumeAllowed;
                vm.ResetAllowed = vmSettings.ResetAllowed;
                vm.RebootAllowed = vmSettings.RebootAllowed;
                vm.ReinstallAllowed = vmSettings.ReinstallAllowed;

                vm.ExternalNetworkEnabled = vmSettings.ExternalNetworkEnabled;
                vm.PrivateNetworkEnabled = vmSettings.PrivateNetworkEnabled;
                /////////////////////////////////////////////

                // dynamic memory
                if (vmSettings.DynamicMemory != null && vmSettings.DynamicMemory.Enabled)
                    vm.DynamicMemory = vmSettings.DynamicMemory;
                else
                    vm.DynamicMemory = null;

                // load service settings
                StringDictionary settings = ServerController.GetServiceSettings(vm.ServiceId);

                #region setup external network
                if (vm.ExternalNetworkEnabled
                    && String.IsNullOrEmpty(vm.ExternalNicMacAddress))
                {
                    // connect to network
                    vm.ExternalSwitchId = settings["ExternalNetworkId"];
                    vm.ExternalNicMacAddress = GenerateMacAddress();
                }
                #endregion

                #region setup private network
                if (vm.PrivateNetworkEnabled
                    && String.IsNullOrEmpty(vm.PrivateNicMacAddress))
                {
                    // connecto to network
                    vm.PrivateSwitchId = settings["PrivateNetworkId"];

                    if (String.IsNullOrEmpty(vm.PrivateSwitchId))
                    {
                        // create/load private virtual switch
                        vm.PrivateSwitchId = EnsurePrivateVirtualSwitch(vm);
                    }
                    vm.PrivateNicMacAddress = GenerateMacAddress();
                }
                #endregion

                // update configuration on virtualization server
                vm = vs.UpdateVirtualMachine(vm);

                // update meta item
                PackageController.UpdatePackageItem(vm);

                // unprovision external IP addresses
                if (!vm.ExternalNetworkEnabled)
                    ServerController.DeleteItemIPAddresses(itemId);
                else
                    // send KVP config items
                    SendNetworkAdapterKVP(itemId, "External");

                // unprovision private IP addresses
                if (!vm.PrivateNetworkEnabled)
                    DataProvider.DeleteItemPrivateIPAddresses(SecurityContext.User.UserId, itemId);
                else
                    // send KVP config items
                    SendNetworkAdapterKVP(itemId, "Private");

                // start if required
                if (wasStarted)
                {
                    result = vs.ChangeVirtualMachineState(vm.VirtualMachineId, VirtualMachineRequestedState.Start);
                    if (!JobCompleted(vs, result.Job))
                    {
                        LogJobResult(res, result.Job);
                        TaskManager.CompleteResultTask(res);
                        return res;
                    }
                }
            }
            catch (Exception ex)
            {
                TaskManager.CompleteResultTask(res, VirtualizationErrorCodes.CHANGE_VM_CONFIGURATION, ex);
                return res;
            }

            TaskManager.CompleteResultTask();
            return res;
        }
        //[Obsolete("UpdateVirtualMachineConfiguration is deprecated, please use UpdateVirtualMachineResource instead.")]
        public static ResultObject UpdateVirtualMachineConfiguration(
            int itemId, int cpuCores, int ramMB, int hddGB, int snapshots,
            bool dvdInstalled, bool bootFromCD, bool numLock, bool startShutdownAllowed, bool pauseResumeAllowed, 
            bool rebootAllowed, bool resetAllowed, bool reinstallAllowed, bool externalNetworkEnabled, bool privateNetworkEnabled, VirtualMachine otherSettings)
        {
            otherSettings.CpuCores = cpuCores;
            otherSettings.RamSize = ramMB;
            otherSettings.HddSize = hddGB;
            otherSettings.SnapshotsNumber = snapshots;

            otherSettings.BootFromCD = bootFromCD;
            otherSettings.NumLockEnabled = numLock;
            otherSettings.DvdDriveInstalled = dvdInstalled;

            otherSettings.StartTurnOffAllowed = startShutdownAllowed;
            otherSettings.PauseResumeAllowed = pauseResumeAllowed;
            otherSettings.ResetAllowed = resetAllowed;
            otherSettings.RebootAllowed = rebootAllowed;
            otherSettings.ReinstallAllowed = reinstallAllowed;

            otherSettings.ExternalNetworkEnabled = externalNetworkEnabled;
            otherSettings.PrivateNetworkEnabled = privateNetworkEnabled;
            return UpdateVirtualMachineResource(itemId, otherSettings);
        }
        #endregion

        #region VNC
        public static string GetVirtualMachineGuacamoleURL(int itemId)
        {
            VirtualMachine vm = GetVirtualMachineByItemId(itemId);
            string vncurl = GuacaHelper.GetUrl(vm);

            return vncurl;
        }
        #endregion

        #region DVD
        public static LibraryItem GetInsertedDvdDisk(int itemId)
        {
            // load item
            VirtualMachine vm = GetVirtualMachineByItemId(itemId);

            // get proxy
            VirtualizationServer2012 vs = GetVirtualizationProxy(vm.ServiceId);
            string isoPath = vs.GetInsertedDVD(vm.VirtualMachineId);

            if (String.IsNullOrEmpty(isoPath))
                return null;

            // load library items
            LibraryItem[] disks = GetLibraryDisks(itemId);

            // find required disk
            isoPath = Path.GetFileName(isoPath);
            foreach (LibraryItem disk in disks)
            {
                if (String.Compare(isoPath, disk.Path, true) == 0)
                    return disk;
            }
            return null;
        }

        public static LibraryItem[] GetLibraryDisks(int itemId)
        {
            // load item
            VirtualMachine vm = GetVirtualMachineByItemId(itemId);

            // load service settings
            StringDictionary settings = ServerController.GetServiceSettings(vm.ServiceId);
            string xml = settings["DvdLibrary"];

            var config = new ConfigFile(xml);

            return config.LibraryItems;
        }

        public static ResultObject InsertDvdDisk(int itemId, string isoPath)
        {
            ResultObject res = new ResultObject();

            // load service item
            VirtualMachine vm = (VirtualMachine)PackageController.GetPackageItem(itemId);
            if (vm == null)
            {
                res.ErrorCodes.Add(VirtualizationErrorCodes.CANNOT_FIND_VIRTUAL_MACHINE_META_ITEM);
                return res;
            }

            #region Check account and space statuses
            // check account
            if (!SecurityContext.CheckAccount(res, DemandAccount.NotDemo | DemandAccount.IsActive))
                return res;

            // check package
            if (!SecurityContext.CheckPackage(res, vm.PackageId, DemandPackage.IsActive))
                return res;
            #endregion

            // start task
            res = TaskManager.StartResultTask<ResultObject>("VPS", "INSERT_DVD_DISK", vm.Id, vm.Name, vm.PackageId);

            try
            {
                // load service settings
                StringDictionary settings = ServerController.GetServiceSettings(vm.ServiceId);
                string libPath = settings["DvdLibraryPath"];

                // combine full path
                string fullPath = Path.Combine(libPath, isoPath);

                // get proxy
                VirtualizationServer2012 vs = GetVirtualizationProxy(vm.ServiceId);

                // insert DVD
                JobResult result = vs.InsertDVD(vm.VirtualMachineId, fullPath);
                if (result.ReturnValue != ReturnCode.OK)
                {
                    LogReturnValueResult(res, result);
                    TaskManager.CompleteResultTask(res);
                    return res;
                }
            }
            catch (Exception ex)
            {
                TaskManager.CompleteResultTask(res, VirtualizationErrorCodes.INSERT_DVD_DISK_ERROR, ex);
                return res;
            }

            TaskManager.CompleteResultTask();
            return res;
        }

        public static ResultObject EjectDvdDisk(int itemId)
        {
            ResultObject res = new ResultObject();

            // load service item
            VirtualMachine vm = (VirtualMachine)PackageController.GetPackageItem(itemId);
            if (vm == null)
            {
                res.ErrorCodes.Add(VirtualizationErrorCodes.CANNOT_FIND_VIRTUAL_MACHINE_META_ITEM);
                return res;
            }

            #region Check account and space statuses
            // check account
            if (!SecurityContext.CheckAccount(res, DemandAccount.NotDemo | DemandAccount.IsActive))
                return res;

            // check package
            if (!SecurityContext.CheckPackage(res, vm.PackageId, DemandPackage.IsActive))
                return res;
            #endregion

            // start task
            res = TaskManager.StartResultTask<ResultObject>("VPS", "EJECT_DVD_DISK", vm.Id, vm.Name, vm.PackageId);

            try
            {
                // get proxy
                VirtualizationServer2012 vs = GetVirtualizationProxy(vm.ServiceId);

                // insert DVD
                JobResult result = vs.EjectDVD(vm.VirtualMachineId);
                if (result.ReturnValue != ReturnCode.OK)
                {
                    LogReturnValueResult(res, result);
                    TaskManager.CompleteResultTask(res);
                    return res;
                }
            }
            catch (Exception ex)
            {
                TaskManager.CompleteResultTask(res, VirtualizationErrorCodes.EJECT_DVD_DISK_ERROR, ex);
                return res;
            }

            TaskManager.CompleteResultTask();
            return res;
        }
        #endregion

        #region Snaphosts
        public static VirtualMachineSnapshot[] GetVirtualMachineSnapshots(int itemId)
        {
            // load service item
            VirtualMachine vm = (VirtualMachine)PackageController.GetPackageItem(itemId);
            if (vm == null)
                return null;

            // get proxy
            VirtualizationServer2012 vs = GetVirtualizationProxy(vm.ServiceId);
            return vs.GetVirtualMachineSnapshots(vm.VirtualMachineId);
        }

        public static VirtualMachineSnapshot GetSnapshot(int itemId, string snaphostId)
        {
            // load service item
            VirtualMachine vm = (VirtualMachine)PackageController.GetPackageItem(itemId);
            if (vm == null)
                return null;

            // get proxy
            VirtualizationServer2012 vs = GetVirtualizationProxy(vm.ServiceId);
            return vs.GetSnapshot(snaphostId);
        }

        public static ResultObject CreateSnapshot(int itemId)
        {
            ResultObject res = new ResultObject();

            // load service item
            VirtualMachine vm = (VirtualMachine)PackageController.GetPackageItem(itemId);
            if (vm == null)
            {
                res.ErrorCodes.Add(VirtualizationErrorCodes.CANNOT_FIND_VIRTUAL_MACHINE_META_ITEM);
                return res;
            }

            #region Check account and space statuses
            // check account
            if (!SecurityContext.CheckAccount(res, DemandAccount.NotDemo | DemandAccount.IsActive))
                return res;

            // check package
            if (!SecurityContext.CheckPackage(res, vm.PackageId, DemandPackage.IsActive))
                return res;
            #endregion

            // start task
            res = TaskManager.StartResultTask<ResultObject>("VPS", "TAKE_SNAPSHOT", vm.Id, vm.Name, vm.PackageId);

            try
            {
                // get proxy
                VirtualizationServer2012 vs = GetVirtualizationProxy(vm.ServiceId);

                #region Check Quotas
                // check quotas
                List<string> quotaResults = new List<string>();
                PackageContext cntx = PackageController.GetPackageContext(vm.PackageId);

                                // check the number of created snapshots
                int createdNumber = vs.GetVirtualMachineSnapshots(vm.VirtualMachineId).Length;
                if (createdNumber >= vm.SnapshotsNumber)
                {
                    TaskManager.CompleteResultTask(res, VirtualizationErrorCodes.QUOTA_EXCEEDED_SNAPSHOTS + ":" + vm.SnapshotsNumber);
                    return res;
                }
                #endregion

                // take snapshot
                JobResult result = vs.CreateSnapshot(vm.VirtualMachineId);
                if (result.ReturnValue != ReturnCode.JobStarted)
                {
                    LogReturnValueResult(res, result);
                    TaskManager.CompleteResultTask(res);
                    return res;
                }

                if (!JobCompleted(vs, result.Job))
                {
                    LogJobResult(res, result.Job);
                    TaskManager.CompleteResultTask(res);
                    return res;
                }
            }
            catch (Exception ex)
            {
                TaskManager.CompleteResultTask(res, VirtualizationErrorCodes.TAKE_SNAPSHOT_ERROR, ex);
                return res;
            }

            TaskManager.CompleteResultTask();
            return res;
        }

        public static ResultObject ApplySnapshot(int itemId, string snapshotId)
        {
            ResultObject res = new ResultObject();

            // load service item
            VirtualMachine vm = (VirtualMachine)PackageController.GetPackageItem(itemId);
            if (vm == null)
            {
                res.ErrorCodes.Add(VirtualizationErrorCodes.CANNOT_FIND_VIRTUAL_MACHINE_META_ITEM);
                return res;
            }

            #region Check account and space statuses
            // check account
            if (!SecurityContext.CheckAccount(res, DemandAccount.NotDemo | DemandAccount.IsActive))
                return res;

            // check package
            if (!SecurityContext.CheckPackage(res, vm.PackageId, DemandPackage.IsActive))
                return res;
            #endregion

            // start task
            res = TaskManager.StartResultTask<ResultObject>("VPS", "APPLY_SNAPSHOT", vm.Id, vm.Name, vm.PackageId);

            try
            {
                JobResult result = null;

                // get proxy
                VirtualizationServer2012 vs = GetVirtualizationProxy(vm.ServiceId);

                // check VM state
                VirtualMachine vps = vs.GetVirtualMachine(vm.VirtualMachineId);

                // stop virtual machine
                if (vps.State != VirtualMachineState.Off)
                {
                    result = vs.ChangeVirtualMachineState(vm.VirtualMachineId, VirtualMachineRequestedState.TurnOff);
                    if (!JobCompleted(vs, result.Job))
                    {
                        LogJobResult(res, result.Job);
                        TaskManager.CompleteResultTask(res);
                        return res;
                    }
                }

                // take snapshot
                result = vs.ApplySnapshot(vm.VirtualMachineId, snapshotId);
                if (result.ReturnValue != ReturnCode.OK)
                {
                    LogReturnValueResult(res, result);
                    TaskManager.CompleteResultTask(res);
                    return res;
                }
            }
            catch (Exception ex)
            {
                TaskManager.CompleteResultTask(res, VirtualizationErrorCodes.APPLY_SNAPSHOT_ERROR, ex);
                return res;
            }

            TaskManager.CompleteResultTask();
            return res;
        }

        public static ResultObject RenameSnapshot(int itemId, string snapshotId, string newName)
        {
            ResultObject res = new ResultObject();

            // load service item
            VirtualMachine vm = (VirtualMachine)PackageController.GetPackageItem(itemId);
            if (vm == null)
            {
                res.ErrorCodes.Add(VirtualizationErrorCodes.CANNOT_FIND_VIRTUAL_MACHINE_META_ITEM);
                return res;
            }

            #region Check account and space statuses
            // check account
            if (!SecurityContext.CheckAccount(res, DemandAccount.NotDemo | DemandAccount.IsActive))
                return res;

            // check package
            if (!SecurityContext.CheckPackage(res, vm.PackageId, DemandPackage.IsActive))
                return res;
            #endregion

            // start task
            res = TaskManager.StartResultTask<ResultObject>("VPS", "RENAME_SNAPSHOT", vm.Id, vm.Name, vm.PackageId);

            try
            {
                // get proxy
                VirtualizationServer2012 vs = GetVirtualizationProxy(vm.ServiceId);

                // take snapshot
                JobResult result = vs.RenameSnapshot(vm.VirtualMachineId, snapshotId, newName);
                if (result.ReturnValue != ReturnCode.OK)
                {
                    LogReturnValueResult(res, result);
                    TaskManager.CompleteResultTask(res);
                    return res;
                }
            }
            catch (Exception ex)
            {
                TaskManager.CompleteResultTask(res, VirtualizationErrorCodes.RENAME_SNAPSHOT_ERROR, ex);
                return res;
            }

            TaskManager.CompleteResultTask();
            return res;
        }

        public static ResultObject DeleteSnapshot(int itemId, string snapshotId)
        {
            ResultObject res = new ResultObject();

            // load service item
            VirtualMachine vm = (VirtualMachine)PackageController.GetPackageItem(itemId);
            if (vm == null)
            {
                res.ErrorCodes.Add(VirtualizationErrorCodes.CANNOT_FIND_VIRTUAL_MACHINE_META_ITEM);
                return res;
            }

            #region Check account and space statuses
            // check account
            if (!SecurityContext.CheckAccount(res, DemandAccount.NotDemo | DemandAccount.IsActive))
                return res;

            // check package
            if (!SecurityContext.CheckPackage(res, vm.PackageId, DemandPackage.IsActive))
                return res;
            #endregion

            // start task
            res = TaskManager.StartResultTask<ResultObject>("VPS", "DELETE_SNAPSHOT", vm.Id, vm.Name, vm.PackageId);

            try
            {
                // get proxy
                VirtualizationServer2012 vs = GetVirtualizationProxy(vm.ServiceId);

                // take snapshot
                JobResult result = vs.DeleteSnapshot(snapshotId);
                if (result.ReturnValue != ReturnCode.JobStarted)
                {
                    LogReturnValueResult(res, result);
                    TaskManager.CompleteResultTask(res);
                    return res;
                }

                if (!JobCompleted(vs, result.Job))
                {
                    LogJobResult(res, result.Job);
                    TaskManager.CompleteResultTask(res);
                    return res;
                }
            }
            catch (Exception ex)
            {
                TaskManager.CompleteResultTask(res, VirtualizationErrorCodes.DELETE_SNAPSHOT_ERROR, ex);
                return res;
            }

            TaskManager.CompleteResultTask();
            return res;
        }

        public static ResultObject DeleteSnapshotSubtree(int itemId, string snapshotId)
        {
            ResultObject res = new ResultObject();

            // load service item
            VirtualMachine vm = (VirtualMachine)PackageController.GetPackageItem(itemId);
            if (vm == null)
            {
                res.ErrorCodes.Add(VirtualizationErrorCodes.CANNOT_FIND_VIRTUAL_MACHINE_META_ITEM);
                return res;
            }

            #region Check account and space statuses
            // check account
            if (!SecurityContext.CheckAccount(res, DemandAccount.NotDemo | DemandAccount.IsActive))
                return res;

            // check package
            if (!SecurityContext.CheckPackage(res, vm.PackageId, DemandPackage.IsActive))
                return res;
            #endregion

            // start task
            res = TaskManager.StartResultTask<ResultObject>("VPS", "DELETE_SNAPSHOT_SUBTREE", vm.Id, vm.Name, vm.PackageId);

            try
            {
                // get proxy
                VirtualizationServer2012 vs = GetVirtualizationProxy(vm.ServiceId);

                // take snapshot
                JobResult result = vs.DeleteSnapshotSubtree(snapshotId);
                if (result.ReturnValue != ReturnCode.JobStarted)
                {
                    LogReturnValueResult(res, result);
                    TaskManager.CompleteResultTask(res);
                    return res;
                }

                if (!JobCompleted(vs, result.Job))
                {
                    LogJobResult(res, result.Job);
                    TaskManager.CompleteResultTask(res);
                    return res;
                }
            }
            catch (Exception ex)
            {
                TaskManager.CompleteResultTask(res, VirtualizationErrorCodes.DELETE_SNAPSHOT_SUBTREE_ERROR, ex);
                return res;
            }

            TaskManager.CompleteResultTask();
            return res;
        }

        public static byte[] GetSnapshotThumbnail(int itemId, string snapshotId, ThumbnailSize size)
        {
            // load service item
            VirtualMachine vm = (VirtualMachine)PackageController.GetPackageItem(itemId);
            if (vm == null)
                return null;

            // get proxy
            VirtualizationServer2012 vs = GetVirtualizationProxy(vm.ServiceId);

            return vs.GetSnapshotThumbnailImage(snapshotId, size);
        }
        #endregion

        #region Network - External
        public static AvailableVLANList GetAvailableVLANs(int PackageId)
        {
            throw new NotImplementedException();
            /*
            AvailableVLANList vlanlist = new AvailableVLANList();
            List<int> vlans = new List<int>();
            try
            {

                List<PackageIPAddress> packageips = ServerController.GetPackageUnassignedIPAddresses(PackageId, IPAddressPool.VpsExternalNetwork);
                foreach (PackageIPAddress ip in packageips)
                {
                    vlans.Add(ip.VLAN);
                }

                // return vlan list without dupes
                vlanlist.vlans = vlans.Distinct().ToList();
            }
            catch (Exception ex)
            {
                TaskManager.WriteError(ex, "VPS_GET_VLAN_ERROR");
            }
            return vlanlist;
            */
        }

        public static int GetExternalNetworkVLAN(int itemId)
        {
            int adaptervlan = DEFAULT_VLAN;
            VirtualMachine vm = null;
            try
            {
                VirtualMachine vmgeneral = GetVirtualMachineGeneralDetails(itemId);
                vm = GetVirtualMachineExtendedInfo(vmgeneral.ServiceId, vmgeneral.VirtualMachineId);
                vm.ExternalNicMacAddress = vmgeneral.ExternalNicMacAddress;
            }
            catch (Exception ex)
            {
                TaskManager.WriteError(ex, "VPS_GET_VM_DETAILS");
            }
            if (vm != null)
            {
                bool firstadapter = true;
                foreach (VirtualMachineNetworkAdapter adapter in vm.Adapters)
                {
                    if (firstadapter)
                    {
                        firstadapter = false;
                        adaptervlan = adapter.vlan;
                    }
                    // Overwrite First Adapter by Mac Match
                    if (adapter.MacAddress == vm.ExternalNicMacAddress)
                    {
                        adaptervlan = adapter.vlan;
                    }
                }
            }
            return adaptervlan;
        }

        public static NetworkAdapterDetails GetExternalNetworkDetails(int packageId)
        {
            // load service
            int serviceId = PackageController.GetPackageServiceId(packageId, ResourceGroups.VPS2012);

            return GetExternalNetworkDetailsInternal(serviceId);
        }

        public static NetworkAdapterDetails GetExternalNetworkAdapterDetails(int itemId)
        {
            // load service item
            VirtualMachine vm = (VirtualMachine)PackageController.GetPackageItem(itemId);
            if (vm == null)
                return null;

            // get default NIC
            NetworkAdapterDetails nic = GetExternalNetworkDetailsInternal(vm.ServiceId);

            // update NIC
            nic.MacAddress = GetSymbolDelimitedMacAddress(vm.ExternalNicMacAddress, "-");

            // load IP addresses
            nic.IPAddresses = ObjectUtils.CreateListFromDataReader<NetworkAdapterIPAddress>(
                DataProvider.GetItemIPAddresses(SecurityContext.User.UserId, itemId, (int)IPAddressPool.VpsExternalNetwork)).ToArray();

            // update subnet CIDR
            foreach (NetworkAdapterIPAddress ip in nic.IPAddresses)
                ip.SubnetMaskCidr = GetSubnetMaskCidr(ip.SubnetMask);

            if (nic.IPAddresses.Length > 0)
            {
                // from primary address
                nic.SubnetMask = nic.IPAddresses[0].SubnetMask;
                nic.SubnetMaskCidr = GetSubnetMaskCidr(nic.SubnetMask);
                nic.DefaultGateway = nic.IPAddresses[0].DefaultGateway;
            }

            return nic;
        }

        public static NetworkAdapterDetails GetManagementNetworkDetails(int packageId)
        {
            // load service
            int serviceId = PackageController.GetPackageServiceId(packageId, ResourceGroups.VPS2012);
            return GetManagementNetworkDetailsInternal(serviceId);
        }

        private static NetworkAdapterDetails GetExternalNetworkDetailsInternal(int serviceId)
        {
            // load service settings
            StringDictionary settings = ServerController.GetServiceSettings(serviceId);

            // create NIC object
            NetworkAdapterDetails nic = new NetworkAdapterDetails();
            nic.NetworkId = settings["ExternalNetworkId"];
            nic.PreferredNameServer = settings["ExternalPreferredNameServer"];
            nic.AlternateNameServer = settings["ExternalAlternateNameServer"];
            return nic;
        }

        public static NetworkAdapterDetails GetManagementNetworkAdapterDetails(int itemId)
        {
            // load service item
            VirtualMachine vm = (VirtualMachine)PackageController.GetPackageItem(itemId);
            if (vm == null)
                return null;

            // get default NIC
            NetworkAdapterDetails nic = GetManagementNetworkDetailsInternal(vm.ServiceId);

            // update NIC
            nic.MacAddress = GetSymbolDelimitedMacAddress(vm.ManagementNicMacAddress, "-");

            // load IP addresses
            nic.IPAddresses = ObjectUtils.CreateListFromDataReader<NetworkAdapterIPAddress>(
                DataProvider.GetItemIPAddresses(SecurityContext.User.UserId, itemId, (int)IPAddressPool.VpsManagementNetwork)).ToArray();

            // update subnet CIDR
            foreach (NetworkAdapterIPAddress ip in nic.IPAddresses)
                ip.SubnetMaskCidr = GetSubnetMaskCidr(ip.SubnetMask);

            if (nic.IPAddresses.Length > 0)
            {
                // from primary address
                nic.SubnetMask = nic.IPAddresses[0].SubnetMask;
                nic.SubnetMaskCidr = GetSubnetMaskCidr(nic.SubnetMask);
                nic.DefaultGateway = nic.IPAddresses[0].DefaultGateway;
            }

            return nic;
        }

        private static NetworkAdapterDetails GetManagementNetworkDetailsInternal(int serviceId)
        {
            // load service settings
            StringDictionary settings = ServerController.GetServiceSettings(serviceId);

            // create NIC object
            NetworkAdapterDetails nic = new NetworkAdapterDetails();
            nic.NetworkId = settings["ManagementNetworkId"];
            nic.IsDHCP = (String.Compare(settings["ManagementNicConfig"], "DHCP", true) == 0);

            if (!nic.IsDHCP)
            {
                nic.PreferredNameServer = settings["ManagementPreferredNameServer"];
                nic.AlternateNameServer = settings["ManagementAlternateNameServer"];
            }
            return nic;
        }

        public static ResultObject AddVirtualMachineExternalIPAddresses(int itemId, bool selectRandom, int addressesNumber, int[] addressIds, bool provisionKvp)
        {
            return AddVirtualMachineInternalIPAddresses(itemId, selectRandom, addressesNumber, addressIds, provisionKvp, -1);
        }

        public static ResultObject AddVirtualMachineInternalIPAddresses(int itemId, bool selectRandom, int addressesNumber, int[] addressIds, bool provisionKvp, int vlan)
        {
            if (addressIds == null)
                throw new ArgumentNullException("addressIds");

            ResultObject res = new ResultObject();

            // load service item
            VirtualMachine vm = (VirtualMachine)PackageController.GetPackageItem(itemId);
            if (vm == null)
            {
                res.ErrorCodes.Add(VirtualizationErrorCodes.CANNOT_FIND_VIRTUAL_MACHINE_META_ITEM);
                return res;
            }

            #region Check account and space statuses
            // check account
            if (!SecurityContext.CheckAccount(res, DemandAccount.NotDemo | DemandAccount.IsActive))
                return res;

            // check package
            if (!SecurityContext.CheckPackage(res, vm.PackageId, DemandPackage.IsActive))
                return res;
            #endregion

            // start task
            res = TaskManager.StartResultTask<ResultObject>("VPS", "ADD_EXTERNAL_IP", vm.Id, vm.Name, vm.PackageId);

            // Get VLAN of 1st Network Interface
            if (vlan == -1)
                vlan = GetExternalNetworkVLAN(itemId);

            try
            {
                if (selectRandom)
                {
                    List<PackageIPAddress> packageips = ServerController.GetPackageUnassignedIPAddresses(vm.PackageId, IPAddressPool.VpsExternalNetwork);
                    List<PackageIPAddress> ips = new List<PackageIPAddress>();
                    foreach (PackageIPAddress ip in packageips)
                    {
                        if (ip.VLAN == vlan)
                        {
                            ips.Add(ip);
                        }
                    }
                    if (addressesNumber > ips.Count)
                    {
                        TaskManager.CompleteResultTask(res, VirtualizationErrorCodes.NOT_ENOUGH_PACKAGE_IP_ADDRESSES);
                        return res;
                    }

                    // get next N unassigned addresses
                    addressIds = new int[addressesNumber];
                    for (int i = 0; i < addressesNumber; i++)
                        addressIds[i] = ips[i].PackageAddressID;
                }

                // add addresses
                foreach (int addressId in addressIds)
                    ServerController.AddItemIPAddress(itemId, addressId);

                // send KVP config items
                if (provisionKvp)
                    SendNetworkAdapterKVP(itemId, "External");
            }
            catch (Exception ex)
            {
                TaskManager.CompleteResultTask(res, VirtualizationErrorCodes.ADD_VIRTUAL_MACHINE_EXTERNAL_IP_ADDRESS_ERROR, ex);
                return res;
            }

            TaskManager.CompleteResultTask();
            return res;
        }

        public static ResultObject SetVirtualMachinePrimaryExternalIPAddress(int itemId, int packageAddressId, bool provisionKvp)
        {
            ResultObject res = new ResultObject();

            // load service item
            VirtualMachine vm = (VirtualMachine)PackageController.GetPackageItem(itemId);
            if (vm == null)
            {
                res.ErrorCodes.Add(VirtualizationErrorCodes.CANNOT_FIND_VIRTUAL_MACHINE_META_ITEM);
                return res;
            }

            #region Check account and space statuses
            // check account
            if (!SecurityContext.CheckAccount(res, DemandAccount.NotDemo | DemandAccount.IsActive))
                return res;

            // check package
            if (!SecurityContext.CheckPackage(res, vm.PackageId, DemandPackage.IsActive))
                return res;
            #endregion

            // start task
            res = TaskManager.StartResultTask<ResultObject>("VPS", "SET_PRIMARY_EXTERNAL_IP", vm.Id, vm.Name, vm.PackageId);

            try
            {
                // call database
                ServerController.SetItemPrimaryIPAddress(itemId, packageAddressId);

                // send KVP config items
                if(provisionKvp)
                    SendNetworkAdapterKVP(itemId, "External");
            }
            catch (Exception ex)
            {
                TaskManager.CompleteResultTask(res, VirtualizationErrorCodes.SET_VIRTUAL_MACHINE_PRIMARY_EXTERNAL_IP_ADDRESS_ERROR, ex);
                return res;
            }

            TaskManager.CompleteResultTask();
            return res;
        }

        public static ResultObject DeleteVirtualMachineExternalIPAddresses(int itemId, int[] packageAddressIds, bool provisionKvp)
        {
            if (packageAddressIds == null)
                throw new ArgumentNullException("addressIds");

            ResultObject res = new ResultObject();

            // load service item
            VirtualMachine vm = (VirtualMachine)PackageController.GetPackageItem(itemId);
            if (vm == null)
            {
                res.ErrorCodes.Add(VirtualizationErrorCodes.CANNOT_FIND_VIRTUAL_MACHINE_META_ITEM);
                return res;
            }

            #region Check account and space statuses
            // check account
            if (!SecurityContext.CheckAccount(res, DemandAccount.NotDemo | DemandAccount.IsActive))
                return res;

            // check package
            if (!SecurityContext.CheckPackage(res, vm.PackageId, DemandPackage.IsActive))
                return res;
            #endregion

            // start task
            res = TaskManager.StartResultTask<ResultObject>("VPS", "DELETE_EXTERNAL_IP", vm.Id, vm.Name, vm.PackageId);

            try
            {
                // call database
                foreach (int packageAddressId in packageAddressIds)
                    ServerController.DeleteItemIPAddress(itemId, packageAddressId);

                // send KVP config items
                if(provisionKvp)
                    SendNetworkAdapterKVP(itemId, "External");
            }
            catch (Exception ex)
            {
                TaskManager.CompleteResultTask(res, VirtualizationErrorCodes.DELETE_VIRTUAL_MACHINE_EXTERNAL_IP_ADDRESS_ERROR, ex);
                return res;
            }

            TaskManager.CompleteResultTask();
            return res;
        }
        #endregion

        #region Network – Private
        public static NetworkAdapterDetails GetPrivateNetworkDetails(int packageId)
        {
            // load service
            int serviceId = PackageController.GetPackageServiceId(packageId, ResourceGroups.VPS2012);

            return GetPrivateNetworkDetailsInternal(serviceId);
        }

        public static NetworkAdapterDetails GetPrivateNetworkAdapterDetails(int itemId)
        {
            // load service item
            VirtualMachine vm = (VirtualMachine)PackageController.GetPackageItem(itemId);
            if (vm == null)
                return null;

            // load default internal adapter
            NetworkAdapterDetails nic = GetPrivateNetworkDetailsInternal(vm.ServiceId);

            // update NIC
            nic.MacAddress = GetSymbolDelimitedMacAddress(vm.PrivateNicMacAddress, "-");

            // load IP addresses
            nic.IPAddresses = ObjectUtils.CreateListFromDataReader<NetworkAdapterIPAddress>(
                DataProvider.GetItemPrivateIPAddresses(SecurityContext.User.UserId, itemId)).ToArray();

            foreach (NetworkAdapterIPAddress ip in nic.IPAddresses)
            {
                ip.SubnetMask = nic.SubnetMask;
                ip.SubnetMaskCidr = nic.SubnetMaskCidr;
                ip.DefaultGateway = nic.DefaultGateway;
            }

            return nic;
        }

        private static NetworkAdapterDetails GetPrivateNetworkDetailsInternal(int serviceId)
        {
            // load service settings
            StringDictionary settings = ServerController.GetServiceSettings(serviceId);

            // create NIC object
            NetworkAdapterDetails nic = new NetworkAdapterDetails();

            string networkFormat = settings["PrivateNetworkFormat"];
            if (String.IsNullOrEmpty(networkFormat))
            {
                // custom format
				nic.NetworkFormat = settings["PrivateIPAddress"];
				var v6 = IPAddress.Parse(nic.NetworkFormat).V6;
                nic.SubnetMask = GetPrivateNetworkSubnetMask(settings["PrivateSubnetMask"], v6);
            }
            else
            {
                // standard format
                string[] formatPair = settings["PrivateNetworkFormat"].Split('/');
                nic.NetworkFormat = formatPair[0];
				var v6 = IPAddress.Parse(nic.NetworkFormat).V6;
				nic.SubnetMask = GetPrivateNetworkSubnetMask(formatPair[1], v6);
            }

            nic.SubnetMaskCidr = GetSubnetMaskCidr(nic.SubnetMask);
            nic.DefaultGateway = settings["PrivateDefaultGateway"];
            nic.PreferredNameServer = settings["PrivatePreferredNameServer"];
            nic.AlternateNameServer = settings["PrivateAlternateNameServer"];

            return nic;
        }

        public static ResultObject AddVirtualMachinePrivateIPAddresses(int itemId, bool selectRandom, int addressesNumber, string[] addresses, bool provisionKvp)
        {
            // trace info
            Trace.TraceInformation("Entering AddVirtualMachinePrivateIPAddresses()");
            Trace.TraceInformation("Item ID: {0}", itemId);
            Trace.TraceInformation("SelectRandom: {0}", selectRandom);
            Trace.TraceInformation("AddressesNumber: {0}", addressesNumber);

            if (addresses != null)
            {
                foreach(var address in addresses)
                    Trace.TraceInformation("addresses[n]: {0}", address);
            }

            ResultObject res = new ResultObject();

            // load service item
            VirtualMachine vm = (VirtualMachine)PackageController.GetPackageItem(itemId);
            if (vm == null)
            {
                res.ErrorCodes.Add(VirtualizationErrorCodes.CANNOT_FIND_VIRTUAL_MACHINE_META_ITEM);
                return res;
            }

            #region Check account and space statuses
            // check account
            if (!SecurityContext.CheckAccount(res, DemandAccount.NotDemo | DemandAccount.IsActive))
                return res;

            // check package
            if (!SecurityContext.CheckPackage(res, vm.PackageId, DemandPackage.IsActive))
                return res;
            #endregion

            // start task
            res = TaskManager.StartResultTask<ResultObject>("VPS", "ADD_PRIVATE_IP", vm.Id, vm.Name, vm.PackageId);

            try
            {
                // load network adapter
                NetworkAdapterDetails nic = GetPrivateNetworkAdapterDetails(itemId);

                bool wasEmptyList = (nic.IPAddresses.Length == 0);

                if(wasEmptyList)
                    Trace.TraceInformation("NIC IP addresses list is empty");

                // check IP addresses if they are specified
                List<string> checkResults = CheckPrivateIPAddresses(vm.PackageId, addresses);
                if (checkResults.Count > 0)
                {
                    res.ErrorCodes.AddRange(checkResults);
                    res.IsSuccess = false;
                    TaskManager.CompleteResultTask();
                    return res;
                }

                // load all existing private IP addresses
                List<PrivateIPAddress> ips = GetPackagePrivateIPAddresses(vm.PackageId);

                // sort them
                SortedList<IPAddress, string> sortedIps = GetSortedNormalizedIPAddresses(ips, nic.SubnetMask);

                if (selectRandom)
                {
                    // generate N number of IP addresses
                    addresses = new string[addressesNumber];
                    for (int i = 0; i < addressesNumber; i++)
                        addresses[i] = GenerateNextAvailablePrivateIP(sortedIps, nic.SubnetMask, nic.NetworkFormat);
                }

                PackageContext cntx = PackageController.GetPackageContext(vm.PackageId);
                QuotaValueInfo quota = cntx.Quotas[Quotas.VPS2012_PRIVATE_IP_ADDRESSES_NUMBER];
                if (quota.QuotaAllocatedValue != -1)
                {
                    int maxAddresses = quota.QuotaAllocatedValue - nic.IPAddresses.Length;

                    if (addresses.Length > maxAddresses)
                    {
                        TaskManager.CompleteResultTask(res, VirtualizationErrorCodes.QUOTA_EXCEEDED_PRIVATE_ADDRESSES_NUMBER + ":" + maxAddresses);
                        return res;
                    }
                }

                // add addresses to database
                foreach (string address in addresses)
                    DataProvider.AddItemPrivateIPAddress(SecurityContext.User.UserId, itemId, address);

                // set primary IP address
                if (wasEmptyList)
                {
                    nic = GetPrivateNetworkAdapterDetails(itemId);
                    if (nic.IPAddresses.Length > 0)
                        SetVirtualMachinePrimaryPrivateIPAddress(itemId, nic.IPAddresses[0].AddressId, false);
                }

                // send KVP config items
                if(provisionKvp)
                    SendNetworkAdapterKVP(itemId, "Private");
            }
            catch (Exception ex)
            {
                TaskManager.CompleteResultTask(res, VirtualizationErrorCodes.ADD_VIRTUAL_MACHINE_PRIVATE_IP_ADDRESS_ERROR, ex);
                return res;
            }

            TaskManager.CompleteResultTask();
            return res;
        }

        private static List<string> CheckPrivateIPAddresses(int packageId, string[] addresses)
        {
            List<string> codes = new List<string>();

            // check IP addresses if they are specified
            if (addresses != null && addresses.Length > 0)
            {
                // load network adapter
                NetworkAdapterDetails nic = GetPrivateNetworkDetails(packageId);

                foreach (string address in addresses)
                {
                    if (!CheckPrivateIPAddress(nic.SubnetMask, address))
                        codes.Add(VirtualizationErrorCodes.WRONG_PRIVATE_IP_ADDRESS_FORMAT + ":" + address);
                }
            }

            return codes;
        }

        public static ResultObject SetVirtualMachinePrimaryPrivateIPAddress(int itemId, int addressId, bool provisionKvp)
        {
            ResultObject res = new ResultObject();

            // load service item
            VirtualMachine vm = (VirtualMachine)PackageController.GetPackageItem(itemId);
            if (vm == null)
            {
                res.ErrorCodes.Add(VirtualizationErrorCodes.CANNOT_FIND_VIRTUAL_MACHINE_META_ITEM);
                return res;
            }

            #region Check account and space statuses
            // check account
            if (!SecurityContext.CheckAccount(res, DemandAccount.NotDemo | DemandAccount.IsActive))
                return res;

            // check package
            if (!SecurityContext.CheckPackage(res, vm.PackageId, DemandPackage.IsActive))
                return res;
            #endregion

            // start task
            res = TaskManager.StartResultTask<ResultObject>("VPS", "SET_PRIMARY_PRIVATE_IP", vm.Id, vm.Name, vm.PackageId);

            try
            {
                // call data access layer
                DataProvider.SetItemPrivatePrimaryIPAddress(SecurityContext.User.UserId, itemId, addressId);

                // send KVP config items
                if(provisionKvp)
                    SendNetworkAdapterKVP(itemId, "Private");
            }
            catch (Exception ex)
            {
                TaskManager.CompleteResultTask(res, VirtualizationErrorCodes.SET_VIRTUAL_MACHINE_PRIMARY_PRIVATE_IP_ADDRESS_ERROR, ex);
                return res;
            }

            TaskManager.CompleteResultTask();
            return res;
        }

        public static ResultObject DeleteVirtualMachinePrivateIPAddresses(int itemId, int[] addressIds, bool provisionKvp)
        {
            if (addressIds == null)
                throw new ArgumentNullException("addressIds");

            ResultObject res = new ResultObject();

            // load service item
            VirtualMachine vm = (VirtualMachine)PackageController.GetPackageItem(itemId);
            if (vm == null)
            {
                res.ErrorCodes.Add(VirtualizationErrorCodes.CANNOT_FIND_VIRTUAL_MACHINE_META_ITEM);
                return res;
            }

            #region Check account and space statuses
            // check account
            if (!SecurityContext.CheckAccount(res, DemandAccount.NotDemo | DemandAccount.IsActive))
                return res;

            // check package
            if (!SecurityContext.CheckPackage(res, vm.PackageId, DemandPackage.IsActive))
                return res;
            #endregion

            // start task
            res = TaskManager.StartResultTask<ResultObject>("VPS", "DELETE_PRIVATE_IP", vm.Id, vm.Name, vm.PackageId);

            try
            {
                // call data access layer
                foreach (int addressId in addressIds)
                    DataProvider.DeleteItemPrivateIPAddress(SecurityContext.User.UserId, itemId, addressId);

                // send KVP config items
                if(provisionKvp)
                    SendNetworkAdapterKVP(itemId, "Private");
            }
            catch (Exception ex)
            {
                TaskManager.CompleteResultTask(res, VirtualizationErrorCodes.DELETE_VIRTUAL_MACHINE_PRIVATE_IP_ADDRESS_ERROR, ex);
                return res;
            }

            TaskManager.CompleteResultTask();
            return res;
        }

        private static string GenerateNextAvailablePrivateIP(SortedList<IPAddress, string> ips, string subnetMask, string startIPAddress)
        {
            Trace.TraceInformation("Entering GenerateNextAvailablePrivateIP()");
            Trace.TraceInformation("Param - number of sorted IPs in the list: {0}", ips.Count);
            Trace.TraceInformation("Param - startIPAddress: {0}", startIPAddress);
            Trace.TraceInformation("Param - subnetMask: {0}", subnetMask);

            // start IP address
            var ip = IPAddress.Parse(startIPAddress) - 1;

            Trace.TraceInformation("Start looking for next available IP");
            foreach (var addr in ips.Keys)
            {
                if ((addr - ip) > 1)
                {
                    // it is a gap
                    break;
                }
                else
                {
                    ip = addr;
                }
            }

            // final IP found
            ip = ip + 1;

            string genIP = ip.ToString();
            Trace.TraceInformation("Generated IP: {0}", genIP);

            // store in cache
            Trace.TraceInformation("Adding to sorted list");
            ips.Add(ip, genIP);

            Trace.TraceInformation("Leaving GenerateNextAvailablePrivateIP()");
            return genIP;
        }

        private static SortedList<IPAddress, string> GetSortedNormalizedIPAddresses(List<PrivateIPAddress> ips, string subnetMask)
        {
            Trace.TraceInformation("Entering GetSortedNormalizedIPAddresses()");
            Trace.TraceInformation("Param - subnetMask: {0}", subnetMask);

            var mask = IPAddress.Parse(subnetMask);
            SortedList<IPAddress, string> sortedIps = new SortedList<IPAddress, string>();
            foreach (PrivateIPAddress ip in ips)
            {
                var addr = IPAddress.Parse(ip.IPAddress);
                sortedIps.Add(addr, ip.IPAddress);

                Trace.TraceInformation("Added {0} to sorted IPs list with key: {1} ", ip.IPAddress, addr.ToString());
            }
            Trace.TraceInformation("Leaving GetSortedNormalizedIPAddresses()");
            return sortedIps;
        }

		private static string GetPrivateNetworkSubnetMask(string cidr, bool v6) {
            if (v6)
            {
                return "/" + cidr;
            }
            else
            {
                return IPAddress.Parse("/" + cidr).ToV4MaskString();
            }
		}

		private static string GetSubnetMaskCidr(string subnetMask) {
			if (String.IsNullOrEmpty(subnetMask))
				return subnetMask;
			var ip = IPAddress.Parse(subnetMask);
			if (ip.V4) {
				int cidr = 32;
				var mask = ip.Address;
				while ((mask & 1) == 0 && cidr > 0) {
					mask >>= 1;
					cidr -= 1;
				}
				return cidr.ToString();
			} else {
				return ip.Cidr.ToString();
			}
		}
		
        private static bool CheckPrivateIPAddress(string subnetMask, string ipAddress)
        {
            var mask = IPAddress.Parse(subnetMask);
            var ip = IPAddress.Parse(ipAddress);

            //return ((mask & ip) == mask);
            return true;
        }
        #endregion

        #region Virtual Machine Permissions
        public static List<VirtualMachinePermission> GetVirtualMachinePermissions(int itemId)
        {
            List<VirtualMachinePermission> result = new List<VirtualMachinePermission>();
            return result;
        }

        public static int UpdateVirtualMachineUserPermissions(int itemId, VirtualMachinePermission[] permissions)
        {
            // VPS - UPDATE_PERMISSIONS
            return 0;
        }
        #endregion

        #region Virtual Switches
        public static VirtualSwitch[] GetExternalSwitches(int serviceId, string computerName)
        {
            VirtualizationServer2012 vs = new VirtualizationServer2012();
            ServiceProviderProxy.Init(vs, serviceId);
            return vs.GetExternalSwitches(computerName);
        }

        public static VirtualSwitch[] GetExternalSwitchesWMI(int serviceId, string computerName)
        {
            VirtualizationServer2012 vs = new VirtualizationServer2012();
            ServiceProviderProxy.Init(vs, serviceId);
            return vs.GetExternalSwitchesWMI(computerName);
        }

        public static VirtualSwitch[] GetInternalSwitches(int serviceId, string computerName)
        {
            VirtualizationServer2012 vs = new VirtualizationServer2012();
            ServiceProviderProxy.Init(vs, serviceId);
            return vs.GetInternalSwitches(computerName);
        }
        #endregion

        #region Tools
        public static ResultObject DeleteVirtualMachine(int itemId, bool saveFiles, bool exportVps, string exportPath) //TODO: Is possible to rework method (Duplicated in server)?
        {
            ResultObject res = new ResultObject();

            #region Maintenance Mode Check
            if (IsMaintenanceMode(itemId))
            {
                res.ErrorCodes.Add(VirtualizationErrorCodes.MAINTENANCE_MODE_IS_ENABLE);
                return res;
            }
            #endregion

            // load service item
            VirtualMachine vm = (VirtualMachine)PackageController.GetPackageItem(itemId);
            if (vm == null)
            {
                res.ErrorCodes.Add(VirtualizationErrorCodes.CANNOT_FIND_VIRTUAL_MACHINE_META_ITEM);
                return res;
            }           

            #region Check account and space statuses
            // check account
            if (!SecurityContext.CheckAccount(res, DemandAccount.NotDemo | DemandAccount.IsActive))
                return res;

            // check package
            if (!SecurityContext.CheckPackage(res, vm.PackageId, DemandPackage.IsActive))
                return res;
            #endregion

            // start task
            res = TaskManager.StartResultTask<ResultObject>("VPS", "DELETE", vm.Id, vm.Name, vm.PackageId);

            try
            {
                // get proxy
                VirtualizationServer2012 vs = GetVirtualizationProxy(vm.ServiceId);

                // check VM state
                VirtualMachine vps = vs.GetVirtualMachine(vm.VirtualMachineId);

                JobResult result = null;

                if (vps != null)
                {
                    #region turn off machine (if required)

                    // stop virtual machine
                    if (vps.State != VirtualMachineState.Off)
                    {
                        TaskManager.Write("VPS_DELETE_TURN_OFF");
                        result = vs.ChangeVirtualMachineState(vm.VirtualMachineId, VirtualMachineRequestedState.TurnOff);
                        // check result
                        if (result.ReturnValue != ReturnCode.JobStarted)
                        {
                            LogReturnValueResult(res, result);
                            TaskManager.CompleteResultTask(res);
                            return res;
                        }

                        // wait for completion
                        if (!JobCompleted(vs, result.Job))
                        {
                            LogJobResult(res, result.Job);
                            TaskManager.CompleteResultTask(res);
                            return res;
                        }
                    }
                    #endregion

                    #region export machine
                    if (exportVps && !String.IsNullOrEmpty(exportPath))
                    {
                        TaskManager.Write("VPS_DELETE_EXPORT");
                        result = vs.ExportVirtualMachine(vm.VirtualMachineId, exportPath);

                        // check result
                        if (result.ReturnValue != ReturnCode.JobStarted)
                        {
                            LogReturnValueResult(res, result);
                            TaskManager.CompleteResultTask(res);
                            return res;
                        }

                        // wait for completion
                        if (!JobCompleted(vs, result.Job))
                        {
                            LogJobResult(res, result.Job);
                            TaskManager.CompleteResultTask(res);
                            return res;
                        }
                    }
                    #endregion

                    #region delete machine
                    TaskManager.Write("VPS_DELETE_DELETE");
                    result = saveFiles ? vs.DeleteVirtualMachine(vm.VirtualMachineId) : vs.DeleteVirtualMachineExtended(vm.VirtualMachineId);

                    // check result
                    if (result.ReturnValue != ReturnCode.JobStarted)
                    {
                        LogReturnValueResult(res, result);
                        TaskManager.CompleteResultTask(res);
                        return res;
                    }

                    // wait for completion
                    if (!JobCompleted(vs, result.Job))
                    {
                        LogJobResult(res, result.Job);
                        TaskManager.CompleteResultTask(res);
                        return res;
                    }
                    #endregion
                }

                #region delete files
                if (!saveFiles)
                {
                    TaskManager.Write("VPS_DELETE_FILES", vm.RootFolderPath);                    
                    try
                    {                       
                        if (vs.IsEmptyFolders(vm.RootFolderPath)) //Prevent a possible hack to delete all files from the Main server :D
                            //not necessarily, we are guaranteed to delete files using DeleteVirtualMachineExtended, left only for deleting folder :)
                            vs.DeleteRemoteFile(vm.RootFolderPath);//TODO: replace by powershell ???
                    }
                    catch (Exception ex)
                    {
                        res.ErrorCodes.Add(VirtualizationErrorCodes.DELETE_VM_FILES_ERROR + ": " + ex.Message);
                    }
                }
                #endregion

                // delete meta item
                PackageController.DeletePackageItem(itemId);
            }
            catch (Exception ex)
            {
                TaskManager.CompleteResultTask(res, VirtualizationErrorCodes.DELETE_ERROR, ex);
                return res;
            }

            TaskManager.CompleteResultTask();
            return res;
        }

        public static ResultObject DeleteVirtualMachineAsynchronous(int itemId, bool saveFiles, bool exportVps, string exportPath)
        {
            ResultObject res = new ResultObject();

            #region Maintenance Mode Check
            if (IsMaintenanceMode(itemId))
            {
                res.ErrorCodes.Add(VirtualizationErrorCodes.MAINTENANCE_MODE_IS_ENABLE);
                return res;
            }
            #endregion

            // load service item
            VirtualMachine vm = (VirtualMachine)PackageController.GetPackageItem(itemId);
            if (vm == null)
            {
                res.ErrorCodes.Add(VirtualizationErrorCodes.CANNOT_FIND_VIRTUAL_MACHINE_META_ITEM);
                return res;
            }
            else if (vm.ProvisioningStatus == VirtualMachineProvisioningStatus.Deleted) //If someone tries to send 1 request twice.
            {
                res.ErrorCodes.Add(VirtualizationErrorCodes.DELETE_ERROR);
                return res;
            }

             #region Check account and space statuses
            // check account
            if (!SecurityContext.CheckAccount(res, DemandAccount.NotDemo | DemandAccount.IsActive))
                return res;

            // check package
            if (!SecurityContext.CheckPackage(res, vm.PackageId, DemandPackage.IsActive))
                return res;
            #endregion

            try
            {
                vm.CurrentTaskId = Guid.NewGuid().ToString("N"); // generate deletion task id
                vm.ProvisioningStatus = VirtualMachineProvisioningStatus.Deleted;
                PackageController.UpdatePackageItem(vm);

                #region Start Asynchronous task
                try
                {
                    VirtualizationAsyncWorker2012 worker = new VirtualizationAsyncWorker2012
                    {
                        ThreadUserId = SecurityContext.User.UserId,
                        Vm = vm,
                        ItemId = itemId,
                        SaveFiles = saveFiles,
                        ExportVps = exportVps,
                        ExportPath = exportPath
                    };
                    worker.DeleteVPSAsync();
                }
                catch (Exception ex)
                {
                    res.AddError(VirtualizationErrorCodes.CREATE_TASK_START_ERROR, ex);
                    return res;
                }
                #endregion
            }
            catch (Exception ex)
            {
                res.AddError(VirtualizationErrorCodes.DELETE_ERROR, ex);
                return res;
            }          

            res.IsSuccess = true;
            return res;
        }

        internal static void DeleteVirtualMachineInternal(int itemId, VirtualMachine vm, bool saveFiles, bool exportVps, string exportPath)
        {
            string taskId = vm.CurrentTaskId;
            // start task
            TaskManager.StartTask(taskId, "VPS", "DELETE", vm.Name, vm.Id, vm.PackageId);            
            
            try
            {
                // get proxy
                VirtualizationServer2012 vs = GetVirtualizationProxy(vm.ServiceId);

                // check VM state
                VirtualMachine vps = vs.GetVirtualMachine(vm.VirtualMachineId);

                JobResult result = null;
                vm.ProvisioningStatus = VirtualMachineProvisioningStatus.InProgress;

                if (vps != null)
                {
                    #region turn off machine (if required)
                    // stop virtual machine
                    if (vps.State != VirtualMachineState.Off)
                    {
                        TaskManager.Write("VPS_DELETE_TURN_OFF");
                        result = vs.ChangeVirtualMachineState(vm.VirtualMachineId, VirtualMachineRequestedState.TurnOff);

                        // check result
                        if (result.ReturnValue != ReturnCode.JobStarted)
                        {
                            TaskManager.WriteError(VirtualizationErrorCodes.JOB_START_ERROR + ":", result.ReturnValue.ToString());
                            return;
                        }
                        // wait for completion
                        if (!JobCompleted(vs, result.Job)) //TODO:
                        {
                            TaskManager.WriteError(VirtualizationErrorCodes.JOB_FAILED_ERROR + ":", result.Job.ErrorDescription.ToString());
                            return;
                        }
                    }
                    #endregion

                    #region export machine
                    if (exportVps && !String.IsNullOrEmpty(exportPath))
                    {
                        TaskManager.Write("VPS_DELETE_EXPORT");
                        result = vs.ExportVirtualMachine(vm.VirtualMachineId, exportPath);

                        // check result
                        if (result.ReturnValue != ReturnCode.JobStarted)
                        {
                            TaskManager.WriteError(VirtualizationErrorCodes.JOB_START_ERROR + ":", result.ReturnValue.ToString());
                            return;
                        }

                        // wait for completion
                        if (!JobCompleted(vs, result.Job))
                        {
                            TaskManager.WriteError(VirtualizationErrorCodes.JOB_FAILED_ERROR + ":", result.Job.ErrorDescription.ToString());
                            return;
                        }
                    }
                    #endregion

                    #region delete machine
                    TaskManager.Write("VPS_DELETE_DELETE");
                    result = saveFiles ? vs.DeleteVirtualMachine(vm.VirtualMachineId) : vs.DeleteVirtualMachineExtended(vm.VirtualMachineId);

                    // check result
                    if (result.ReturnValue != ReturnCode.JobStarted)
                    {
                        TaskManager.WriteError(VirtualizationErrorCodes.JOB_START_ERROR + ":", result.ReturnValue.ToString());
                        return;
                    }
                    // wait for completion
                    if (!JobCompleted(vs, result.Job))
                    {
                        TaskManager.WriteError(VirtualizationErrorCodes.JOB_FAILED_ERROR + ":", result.Job.ErrorDescription.ToString());
                        return;
                    }
                    #endregion
                }

                // mark as deleted
                vm.ProvisioningStatus = VirtualMachineProvisioningStatus.Deleted;

                #region delete Empty folders
                if (!saveFiles)
                {
                    TaskManager.Write("VPS_DELETE_FILES", vm.RootFolderPath);
                    try
                    {
                        if (vs.IsEmptyFolders(vm.RootFolderPath)) //Prevent a possible hack to delete all files from the Main server :D
                            //not necessarily, we are guaranteed to delete files using DeleteVirtualMachineExtended, left only for deleting folder :)
                            vs.DeleteRemoteFile(vm.RootFolderPath);//TODO: replace by powershell ???
                    }
                    catch (Exception ex)
                    {
                        TaskManager.WriteError(ex, VirtualizationErrorCodes.DELETE_VM_FILES_ERROR + ":");
                    }
                }
                #endregion                
            }
            catch (Exception ex)
            {
                TaskManager.WriteError(ex, VirtualizationErrorCodes.DELETE_ERROR + ":");
                return;
            }
            finally
            {
                if(vm.ProvisioningStatus == VirtualMachineProvisioningStatus.Deleted)
                {
                    PackageController.DeletePackageItem(itemId);
                }
                else
                {
                    vm.CurrentTaskId = null;
                    vm.ProvisioningStatus = VirtualMachineProvisioningStatus.Error;
                    PackageController.UpdatePackageItem(vm); //to access the audit log.
                }

                // complete task
                TaskManager.CompleteTask();
            }            
        }

        public static ResultObject ReinstallVirtualMachine(int itemId, VirtualMachine VMSettings, string adminPassword, string[] privIps, 
            bool saveVirtualDisk, bool exportVps, string exportPath)
        {
            ResultObject res = new IntResult();

            #region Maintenance Mode Check
            if (IsMaintenanceMode(itemId))
            {
                res.ErrorCodes.Add(VirtualizationErrorCodes.MAINTENANCE_MODE_IS_ENABLE);
                return res;
            }
            #endregion

            if (string.IsNullOrEmpty(VMSettings.OperatingSystemTemplatePath)) //check if we lose VMSettings 
            {
                int PackageId = VMSettings.PackageId;
                VMSettings = GetVirtualMachineByItemId(itemId);
                if (VMSettings == null)
                {
                    res.ErrorCodes.Add(VirtualizationErrorCodes.CANNOT_FIND_VIRTUAL_MACHINE_META_ITEM);
                    return res;
                }
                VMSettings.OperatingSystemTemplate = Path.GetFileName(VMSettings.OperatingSystemTemplatePath);
                VMSettings.PackageId = PackageId;
            }

            try
            {
                #region Start Asynchronous task
                try
                {
                    VirtualizationAsyncWorker2012 worker = new VirtualizationAsyncWorker2012
                    {
                        ThreadUserId = SecurityContext.User.UserId,
                        Vm = VMSettings,
                        ItemId = itemId,
                        AdminPassword = adminPassword,
                        PrivIps = privIps,
                        SaveFiles = saveVirtualDisk,
                        ExportVps = exportVps,
                        ExportPath = exportPath,                        
                    };
                    worker.ReinstallVPSAsync();
                }
                catch (Exception ex)
                {
                    res.AddError(VirtualizationErrorCodes.CREATE_TASK_START_ERROR, ex);
                    return res;
                }
                #endregion
            }
            catch (Exception ex)
            {
                res.AddError(VirtualizationErrorCodes.DELETE_ERROR, ex);
                return res;
            }

            res.IsSuccess = true;
            return res;
        }
        //
        internal static void ReinstallVirtualMachineInternal(int itemId, VirtualMachine VMSettings, string adminPassword, string[] privIps,
            bool saveVirtualDisk, bool exportVps, string exportPath)
        {
            TaskManager.StartTask("VPS2012", "REINSTALL");

            IntResult result = new IntResult();
            string osTemplateFile = VMSettings.OperatingSystemTemplate;
            TaskManager.Write(String.Format("VPS Operating System Template {0}", osTemplateFile));

            #region Setup IPs
            List<int> extIps = new List<int>();
            byte externalAddressesNumber = 0;
            if (VMSettings.ExternalNetworkEnabled)
            {
                List<int> ipAddressesID = new List<int>();
                externalAddressesNumber = 1;
                NetworkAdapterDetails nic = GetExternalNetworkAdapterDetails(itemId);
                if (nic.IPAddresses != null && nic.IPAddresses.GetLength(0) > 0)
                {
                    foreach (NetworkAdapterIPAddress ip in nic.IPAddresses)
                    {
                        ipAddressesID.Add(ip.AddressId);
                    }                        
                }
                extIps = ipAddressesID;
                //TODO: not needed at the moment, thanks a bug for this :)
                //List<PackageIPAddress> uips = ServerController.GetItemIPAddresses(itemId, IPAddressPool.VpsExternalNetwork);
                //foreach (PackageIPAddress uip in uips)
                //    foreach (int ip in ipAddressesID)
                //        if (ip == uip.AddressID)
                //        {
                //            TaskManager.Write(String.Format("PackageAddressID {0}", uip.AddressID));                                                        
                //            extIps.Add(uip.AddressID); //PIP.PackageAddressID AS AddressID (install_db.sql line 22790), really? It looks like a bug... 
                //                                       //but ok, just for furture if someone fix it, here too need change to uip.PackageAddressID
                //            break;
                //        }
            }

            byte privateAddressesNumber = 0;
            if (VMSettings.PrivateNetworkEnabled && (privIps != null && privIps.Length > 0))
                privateAddressesNumber = 1;            
            #endregion

            ResultObject res = DeleteVirtualMachineAsynchronous(itemId, saveVirtualDisk, exportVps, exportPath);

            if (res.IsSuccess)
            {
                int timeOut = 240;
                while ((VirtualMachine)PackageController.GetPackageItem(itemId) != null && timeOut > 0)
                {
                    System.Threading.Thread.Sleep(1000);
                    timeOut--;
                }
                if (timeOut > 0)
                {
                    TaskManager.Write(String.Format("The old VPS was deleted."));
                    System.Threading.Thread.Sleep(1000); //give a little time to delete, just for sure.                    
                    result = CreateNewVirtualMachine(VMSettings, osTemplateFile, adminPassword, null, externalAddressesNumber, false, extIps.ToArray(), privateAddressesNumber, false, privIps);
                    if (result.IsSuccess)
                    {
                        TaskManager.Write(String.Format("Begin to create a new VPS"));
                    }
                    else
                    {
                        TaskManager.WriteWarning("Error creating server: {0}", result.ErrorCodes.ToArray());
                    }
                }
            }
            TaskManager.CompleteTask();
        }

        //TODO: Add another reinstall method.
        //public static int ReinstallVirtualMachine(int itemId, string adminPassword, bool preserveVirtualDiskFiles,
        //    bool saveVirtualDisk, bool exportVps, string exportPath)
        //{

        //    return 0;
        //}
        #endregion

        #region Help
        public static string GetVirtualMachineSummaryText(int itemId, bool emailMode, bool creation)
        {
            // load item
            VirtualMachine vm = GetVirtualMachineByItemId(itemId);

            // load user info
            UserInfo user = PackageController.GetPackageOwner(vm.PackageId);

            // get letter settings
            UserSettings settings = UserController.GetUserSettings(user.UserId, UserSettings.VPS_SUMMARY_LETTER);

            string settingName = user.HtmlMail ? "HtmlBody" : "TextBody";
            string body = settings[settingName];
            if (String.IsNullOrEmpty(body))
                return null;

            string result = EvaluateVirtualMachineTemplate(itemId, emailMode, creation, body);
            return user.HtmlMail ? result : result.Replace("\n", "<br/>");
        }

        public static ResultObject SendVirtualMachineSummaryLetter(int itemId, string to, string bcc, bool creation)
        {
            ResultObject res = new ResultObject();

            // load service item
            VirtualMachine vm = (VirtualMachine)PackageController.GetPackageItem(itemId);
            if (vm == null)
            {
                res.ErrorCodes.Add(VirtualizationErrorCodes.CANNOT_FIND_VIRTUAL_MACHINE_META_ITEM);
                return res;
            }

            #region Check account and space statuses
            // check account
            if (!SecurityContext.CheckAccount(res, DemandAccount.NotDemo | DemandAccount.IsActive))
                return res;

            // check package
            if (!SecurityContext.CheckPackage(res, vm.PackageId, DemandPackage.IsActive))
                return res;
            #endregion

            // start task
            res = TaskManager.StartResultTask<ResultObject>("VPS", "SEND_SUMMARY_LETTER", vm.Id, vm.Name, vm.PackageId);

            try
            {
                // load user info
                UserInfo user = PackageController.GetPackageOwner(vm.PackageId);

                // get letter settings
                UserSettings settings = UserController.GetUserSettings(user.UserId, UserSettings.VPS_SUMMARY_LETTER);

                string from = settings["From"];
                if (bcc == null)
                    bcc = settings["CC"];
                string subject = settings["Subject"];
                string body = user.HtmlMail ? settings["HtmlBody"] : settings["TextBody"];
                bool isHtml = user.HtmlMail;

                MailPriority priority = MailPriority.Normal;
                if (!String.IsNullOrEmpty(settings["Priority"]))
                    priority = (MailPriority)Enum.Parse(typeof(MailPriority), settings["Priority"], true);

                if (String.IsNullOrEmpty(body))
                {
                    TaskManager.CompleteResultTask(res, VirtualizationErrorCodes.SUMMARY_TEMPLATE_IS_EMPTY);
                    return res;
                }

                // load user info
                if (to == null)
                    to = user.Email;

                subject = EvaluateVirtualMachineTemplate(itemId, true, creation, subject);
                body = EvaluateVirtualMachineTemplate(itemId, true, creation, body);

                // send message
                int result = MailHelper.SendMessage(from, to, bcc, subject, body, priority, isHtml);

                if (result != 0)
                {
                    TaskManager.CompleteResultTask(res, VirtualizationErrorCodes.SEND_SUMMARY_LETTER_CODE + ":" + result);
                    TaskManager.WriteWarning("VPS_SEND_SUMMARY_LETTER_ERROR_CODE", result.ToString());
                    return res;
                }
            }
            catch (Exception ex)
            {
                TaskManager.CompleteResultTask(res, VirtualizationErrorCodes.SEND_SUMMARY_LETTER, ex);
                TaskManager.WriteWarning("VPS_SEND_SUMMARY_LETTER_ERROR", ex.Message);
                return res;
            }

            TaskManager.CompleteResultTask();
            return res;
        }

        public static string EvaluateVirtualMachineTemplate(int itemId, bool emailMode, bool creation, string template)
        {
            Hashtable items = new Hashtable();

            // load machine details
            VirtualMachine vm = GetVirtualMachineByItemId(itemId);
            if (vm == null)
                throw new Exception("VPS with the specified ID was not found.");

            // space info
            PackageInfo package = PackageController.GetPackage(vm.PackageId);
            items["space"] = package;

            // user info
            items["user"] = PackageController.GetPackageOwner(vm.PackageId);

            // VM item
            items["vm"] = vm;

            // load external NIC
            items["external_nic"] = GetExternalNetworkAdapterDetails(itemId);

            // load private NIC
            items["private_nic"] = GetPrivateNetworkAdapterDetails(itemId);

            // load private NIC
            items["management_nic"] = GetManagementNetworkAdapterDetails(itemId);

            // load service settings
            StringDictionary settings = ServerController.GetServiceSettings(vm.ServiceId);

            foreach (string key in settings.Keys)
                items[key] = settings[key];

            // service items
            items["email"] = emailMode;
            items["creation"] = creation;

            // evaluate template
            return PackageController.EvaluateTemplate(template, items);
        }
        #endregion

        #region Helper methods
        private static bool IsMaintenanceMode(int itemId)
        {
            return IsMaintenanceMode(itemId, null);
        }
        private static bool IsMaintenanceMode(StringDictionary settings)
        {
            return IsMaintenanceMode(-1, settings);
        }
        private static bool IsMaintenanceMode(int itemId, StringDictionary settings)
        {
            if(settings == null && itemId != -1)
            {
                // service ID
                int serviceId = GetServiceId(PackageController.GetPackageItem(itemId).PackageId);

                // load service settings
                settings = ServerController.GetServiceSettings(serviceId);
            }            

            bool isMaintenanceMode = settings["MaintenanceMode"] == MAINTENANCE_MODE_EMABLED;

            // Administrator ignore that rule
            return UserController.GetUserInternally(SecurityContext.User.UserId).Role != UserRole.Administrator && isMaintenanceMode;
        }

        private static int GetServiceId(int packageId)
        {
            int serviceId = PackageController.GetPackageServiceId(packageId, ResourceGroups.VPS2012);
            return serviceId;
        }

        private static VirtualizationServer2012 GetVirtualizationProxyByPackageId(int packageId)
        {
            // get service
            int serviceId = GetServiceId(packageId);

            return GetVirtualizationProxy(serviceId);
        }
        
        private static VirtualizationServer2012 GetVirtualizationProxy(int serviceId)
        {
            return VirtualizationHelper.GetVirtualizationProxy(serviceId);
        }

        public static VirtualMachine GetVirtualMachineByItemId(int itemId)
        {
            VirtualMachine vm = (VirtualMachine)PackageController.GetPackageItem(itemId);
            if (vm == null)
                return null;

            // host name
            int dotIdx = vm.Name.IndexOf(".");
            if (dotIdx > -1)
            {
                vm.Hostname = vm.Name.Substring(0, dotIdx);
                vm.Domain = vm.Name.Substring(dotIdx + 1);
            }
            else
            {
                vm.Hostname = vm.Name;
                vm.Domain = "";
            }

            // check if task was aborted during provisioning
            if (!String.IsNullOrEmpty(vm.CurrentTaskId)
                && TaskManager.GetTask(vm.CurrentTaskId) == null)
            {
                // set to error
                vm.CurrentTaskId = null;
                vm.ProvisioningStatus = VirtualMachineProvisioningStatus.Error;
                PackageController.UpdatePackageItem(vm);
            }

            vm.AdministratorPassword = CryptoUtils.Decrypt(vm.AdministratorPassword);
            return vm;
        }

        private static void LogReturnValueResult(ResultObject res, JobResult job)
        {
            res.ErrorCodes.Add(VirtualizationErrorCodes.JOB_START_ERROR + ":" + job.ReturnValue);
        }

        private static void LogJobResult(ResultObject res, ConcreteJob job)
        {
            res.ErrorCodes.Add(VirtualizationErrorCodes.JOB_FAILED_ERROR + ":" + job.ErrorDescription);
        }

        private static bool JobCompleted(VirtualizationServer2012 vs, ConcreteJob job)
        {
            TaskManager.IndicatorMaximum = 100;
            bool jobCompleted = true;
            short timeout = 60;
            while(job.JobState == ConcreteJobState.NotStarted && timeout > 0) //Often jobs are only initialized, need to wait a little, that it started.
            {
                timeout--;
                System.Threading.Thread.Sleep(2000);
                job = vs.GetJob(job.Id);
            }
            
            while (job.JobState == ConcreteJobState.Starting ||
                job.JobState == ConcreteJobState.Running)
            {
                System.Threading.Thread.Sleep(3000);
                job = vs.GetJob(job.Id);
                TaskManager.IndicatorCurrent = job.PercentComplete;
            }

            if (job.JobState != ConcreteJobState.Completed)
            {
                jobCompleted = false;
            }

            TaskManager.IndicatorCurrent = 0;   // reset indicator
            vs.ClearOldJobs();

            return jobCompleted;
        }

        public static string GenerateMacAddress()
        {
            return MS_MAC_PREFIX + Utils.GetRandomHexString(3);
        }

        #endregion


        #region Replication

        #region IsReplicaServer Part

        public static CertificateInfo[] GetCertificates(int serviceId, string remoteServer)
        {
            VirtualizationServer2012 vs = GetVirtualizationProxy(serviceId);
            return vs.GetCertificates(remoteServer);
        }

        public static ResultObject SetReplicaServer(int serviceId, string remoteServer, string thumbprint, string storagePath)
        {
            ResultObject result = new ResultObject();
            try
            {
                if (string.IsNullOrEmpty(storagePath))
                    throw new Exception("Please enter replication path"); 
                
                VirtualizationServer2012 vs = GetVirtualizationProxy(serviceId);
                vs.SetReplicaServer(remoteServer, thumbprint, storagePath);
                result.IsSuccess = true;
            }
            catch (Exception ex)
            {
                result.AddError(VirtualizationErrorCodes.SET_REPLICA_SERVER_ERROR, ex);
            }
            return result;
        }

        public static ResultObject UnsetReplicaServer(int serviceId, string remoteServer)
        {
            ResultObject result = new ResultObject();
            try
            {
                VirtualizationServer2012 vs = GetVirtualizationProxy(serviceId);
                vs.UnsetReplicaServer(remoteServer);
                result.IsSuccess = true;
            }
            catch (Exception ex)
            {
                result.AddError(VirtualizationErrorCodes.UNSET_REPLICA_SERVER_ERROR, ex);
            }
            return result;
        }

        public static ReplicationServerInfo GetReplicaServer(int serviceId, string remoteServer)
        {
            VirtualizationServer2012 vs = GetVirtualizationProxy(serviceId);
            return vs.GetReplicaServer(remoteServer);
        }

        #endregion

        public static VmReplication GetReplication(int itemId)
        {
            VirtualMachine vm = GetVirtualMachineByItemId(itemId);
            VirtualizationServer2012 vs = GetVirtualizationProxy(vm.ServiceId);
            return vs.GetReplication(vm.VirtualMachineId);
        }

        public static ReplicationDetailInfo GetReplicationInfo(int itemId)
        {
            VirtualMachine vm = GetVirtualMachineByItemId(itemId);
            VirtualizationServer2012 vs = GetVirtualizationProxy(vm.ServiceId);
            return vs.GetReplicationInfo(vm.VirtualMachineId);
        }

        public static ResultObject SetVmReplication(int itemId, VmReplication replication)
        {
            TaskManager.StartTask("VPS2012", "SetVmReplication");

            ResultObject result = new ResultObject();


            try
            {
                VirtualMachine vm = GetVirtualMachineByItemId(itemId);

                // Check Quotas
                ReplicationHelper.CheckReplicationQuota(vm.PackageId, ref result);
                if (result.ErrorCodes.Count > 0)
                    return result;

                VirtualizationServer2012 vs = GetVirtualizationProxy(vm.ServiceId);

                // Get replica server
                var replicaServerInfo = ReplicationHelper.GetReplicaInfoForService(vm.ServiceId, ref result);
                if (result.ErrorCodes.Count > 0) return result;

                // We should use enable replication or set replication?
                var vmReplica = vs.GetReplication(vm.VirtualMachineId);
                if (vmReplica == null) // need enable
                {
                    vs.EnableVmReplication(vm.VirtualMachineId, replicaServerInfo.ComputerName, replication);
                    vs.StartInitialReplication(vm.VirtualMachineId);
                }
                else // need set
                {
                    vs.SetVmReplication(vm.VirtualMachineId, replicaServerInfo.ComputerName, replication);
                }
                result.IsSuccess = true;
            }
            catch (Exception ex)
            {
                throw TaskManager.WriteError(ex);
            }
            finally
            {
                TaskManager.CompleteTask();
            }
            TaskManager.WriteWarning("Organization with itemId '{0}' not found", itemId.ToString());
            return result; 
        }

        public static ResultObject DisableVmReplication(int itemId)
        {
            ResultObject result = new ResultObject();
            try
            {
                VirtualMachine vm = GetVirtualMachineByItemId(itemId);

                // Check Quotas
                ReplicationHelper.CheckReplicationQuota(vm.PackageId, ref result);
                if (result.ErrorCodes.Count > 0) return result;

                VirtualizationServer2012 vs = GetVirtualizationProxy(vm.ServiceId);
                vs.DisableVmReplication(vm.VirtualMachineId);

                ReplicationHelper.CleanUpReplicaServer(vm);

                result.IsSuccess = true;
            }
            catch (Exception ex)
            {
                result.AddError(VirtualizationErrorCodes.DISABLE_REPLICATION_ERROR, ex);
            }
            return result; 
        }

        public static ResultObject PauseReplication(int itemId)
        {
            ResultObject result = new ResultObject();
            try
            {
                VirtualMachine vm = GetVirtualMachineByItemId(itemId);

                // Check Quotas
                ReplicationHelper.CheckReplicationQuota(vm.PackageId, ref result);
                if (result.ErrorCodes.Count > 0) return result;

                VirtualizationServer2012 vs = GetVirtualizationProxy(vm.ServiceId);
                vs.PauseReplication(vm.VirtualMachineId);

                result.IsSuccess = true;
            }
            catch (Exception ex)
            {
                result.AddError(VirtualizationErrorCodes.PAUSE_REPLICATION_ERROR, ex);
            }
            return result; 
        }

        public static ResultObject ResumeReplication(int itemId)
        {
            ResultObject result = new ResultObject();
            try
            {
                VirtualMachine vm = GetVirtualMachineByItemId(itemId);

                // Check Quotas
                ReplicationHelper.CheckReplicationQuota(vm.PackageId, ref result);
                if (result.ErrorCodes.Count > 0) return result;

                VirtualizationServer2012 vs = GetVirtualizationProxy(vm.ServiceId);
                vs.ResumeReplication(vm.VirtualMachineId);

                result.IsSuccess = true;
            }
            catch (Exception ex)
            {
                result.AddError(VirtualizationErrorCodes.RESUME_REPLICATION_ERROR, ex);
            }
            return result; 
        }

        #endregion

    }
}
