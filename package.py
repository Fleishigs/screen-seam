from pathlib import Path
import uuid, shutil, subprocess
p=Path(__file__).resolve().parent
d=p/'build-msi'
d.mkdir(exist_ok=True)
def table(name, cols, types, keys, rows):
    lines=[cols.split(),types.split(),[name,*keys.split()],*rows]
    (d/(name+'.idt')).write_bytes(('\r\n'.join('\t'.join(map(str,line)) for line in lines)+'\r\n').encode('ascii'))
guid=lambda:'{'+str(uuid.uuid4()).upper()+'}'
table('Property','Property Value','s72 l0','Property',[
 ('ProductCode',guid()),('ProductName','Screen Seam'),('ProductVersion','2.4.1'),('ProductLanguage','1033'),('Manufacturer','Screen Seam'),('UpgradeCode','{031615CB-B46D-454B-9B5E-E35970649601}'),('ARPNOMODIFY','1'),('ARPPRODUCTICON','ScreenSeam.ico'),('SecureCustomProperties','OLDERSEAM;NEWERSEAM')])
table('Directory','Directory Directory_Parent DefaultDir','s72 S72 l255','Directory',[
 ('TARGETDIR','','SourceDir'),('LocalAppDataFolder','TARGETDIR','.'),('Programs','LocalAppDataFolder','Programs'),('INSTALLDIR','Programs','SCRSEAM|Screen Seam'),('ProgramMenuFolder','TARGETDIR','.')])
table('Component','Component ComponentId Directory_ Attributes Condition KeyPath','s72 S38 s72 i2 S255 S72','Component',[('App','{9153D2C4-7BA5-4628-A634-C6D37D7DA23F}','INSTALLDIR',4,'','AppKey')])
table('Registry','Registry Root Key Name Value Component_','s72 i2 l255 L255 L0 s72','Registry',[('AppKey',1,'Software\\Screen Seam','InstallDir','[INSTALLDIR]','App')])
table('File','File Component_ FileName FileSize Version Language Attributes Sequence','s72 s72 l255 i4 S72 S20 I2 i4','File',[('SeamExe','App','SCRSEAM.EXE|ScreenSeam.exe',(p/'ScreenSeam.exe').stat().st_size,'2.4.1.0','','512','1')])
table('Feature','Feature Feature_Parent Title Description Display Level Directory_ Attributes','s38 S38 L64 L255 I2 i2 S72 i2','Feature',[('Main','','Screen Seam','','1','1','INSTALLDIR','0')])
table('FeatureComponents','Feature_ Component_','s38 s72','Feature_ Component_',[('Main','App')])
table('Media','DiskId LastSequence DiskPrompt Cabinet VolumeLabel Source','i2 i4 L64 S255 S32 S72','DiskId',[(1,1,'','#payload.cab','','')])
table('Shortcut','Shortcut Directory_ Name Component_ Target Arguments Description Hotkey Icon_ IconIndex ShowCmd WkDir','s72 s72 l128 s72 s72 S255 L255 I2 S72 I2 I2 S72','Shortcut',[('StartApp','ProgramMenuFolder','SCRSEAM|Screen Seam','App','[#SeamExe]','','Adjust and stitch your stacked screens','','','','1','INSTALLDIR')])
table('Upgrade','UpgradeCode VersionMin VersionMax Language Attributes Remove ActionProperty','s38 S20 S20 S255 i4 S255 s72','UpgradeCode VersionMin VersionMax Language Attributes',[
 ('{031615CB-B46D-454B-9B5E-E35970649601}','','2.4.1','',1,'','OLDERSEAM'),
 ('{031615CB-B46D-454B-9B5E-E35970649601}','2.4.1','','',2,'','NEWERSEAM')])
table('LaunchCondition','Condition Description','s255 l255','Condition',[('Installed OR NOT NEWERSEAM','A newer version of Screen Seam is installed.')])
seq=[('FindRelatedProducts',25),('LaunchConditions',100),('RemoveExistingProducts',1510),('CostInitialize',800),('FileCost',900),('CostFinalize',1000),('InstallValidate',1400),('InstallInitialize',1500),('ProcessComponents',1600),('UnpublishFeatures',1800),('RemoveRegistryValues',2600),('RemoveShortcuts',3200),('RemoveFiles',3500),('InstallFiles',4000),('CreateShortcuts',4500),('WriteRegistryValues',5000),('RegisterUser',6000),('RegisterProduct',6100),('PublishFeatures',6300),('PublishProduct',6400),('InstallFinalize',6600)]
for name,actions in [('InstallExecuteSequence',seq),('AdminExecuteSequence',[('CostInitialize',800),('FileCost',900),('CostFinalize',1000),('InstallValidate',1400),('InstallInitialize',1500),('InstallAdminPackage',3900),('InstallFiles',4000),('InstallFinalize',6600)])]:
    table(name,'Action Condition Sequence','s72 S255 I2','Action',[(x,'',n) for x,n in actions])
shutil.copyfile(p/'ScreenSeam.exe',d/'SeamExe')
subprocess.run(['makecab','SeamExe','payload.cab'],cwd=d,check=True)

import ctypes as C
from ctypes import wintypes as W
from pathlib import Path
import uuid
msi=C.WinDLL('msi')
u=C.c_uint
def invoke(name,*args):
    rc=getattr(msi,name)(*args)
    if rc: raise RuntimeError((name,rc))
def handle(name,*args):
    h=u();invoke(name,*args,C.byref(h));return h
d=p/'build-msi'
out=str(p/'ScreenSeam-2.4.1-Setup.msi')
db=handle('MsiOpenDatabaseW',out,C.c_void_p(3))
for t in d.glob('*.idt'):invoke('MsiDatabaseImportW',db,str(d),t.name)
q=handle('MsiDatabaseOpenViewW',db,'INSERT INTO `_Streams` (`Name`, `Data`) VALUES (?, ?)')
r=msi.MsiCreateRecord(2);invoke('MsiRecordSetStringW',r,1,'payload.cab');invoke('MsiRecordSetStreamW',r,2,str(d/'payload.cab'));invoke('MsiViewExecute',q,r)
q=handle('MsiDatabaseOpenViewW',db,'CREATE TABLE `Icon` (`Name` CHAR(72) NOT NULL, `Data` OBJECT NOT NULL PRIMARY KEY `Name`)')
invoke('MsiViewExecute',q,0)
q=handle('MsiDatabaseOpenViewW',db,'INSERT INTO `Icon` (`Name`, `Data`) VALUES (?, ?)')
r=msi.MsiCreateRecord(2);invoke('MsiRecordSetStringW',r,1,'ScreenSeam.ico');invoke('MsiRecordSetStreamW',r,2,str(p/'screen-seam.ico'));invoke('MsiViewExecute',q,r)
s=handle('MsiGetSummaryInformationW',db,None,20)
for k,v in {2:'Installation Database',3:'Screen Seam',4:'Screen Seam',7:'Intel;1033',9:'{'+str(uuid.uuid4()).upper()+'}',14:200,15:10}.items():
    invoke('MsiSummaryInfoSetPropertyW',s,k,3 if isinstance(v,int) else 30,v if isinstance(v,int) else 0,None,None if isinstance(v,int) else v)
invoke('MsiSummaryInfoPersist',s);invoke('MsiDatabaseCommit',db)
