// GROUP-STORAGE-TRANSITION: migration audit workbook. Remove this tool after final acceptance and archive the snapshots.
import fs from 'node:fs/promises';
import path from 'node:path';
import {Workbook, SpreadsheetFile} from '@oai/artifact-tool';

const directory=path.resolve(process.argv[2] ?? 'outputs/group-migration-audit-20261101');
const source=JSON.parse(await fs.readFile(path.join(directory,'source.json'),'utf8'));
if(!source.readOnly || source.outcome!=='rolled back') throw new Error('Expected a completed read-only snapshot');
const rows=source.metrics;
if(!rows.length || rows.length>=20000) throw new Error('Empty or truncated export');
if(new Set(rows.map(r=>r.conversation_id)).size!==rows.length) throw new Error('Duplicate conversation IDs');
for(const r of rows) if(r.total_messages!==r.visible_messages+r.deleted_messages) throw new Error('Message totals do not reconcile');
const roots=new Map();
for(const r of rows){
 if(!roots.has(r.root_id)) roots.set(r.root_id,{code:r.request_code,colour:r.colour_code,groups:0,mapped:0,bad:0,types:[0,0,0,0],visible:0,deleted:0,total:0});
 const v=roots.get(r.root_id);v.groups++;v.mapped+=r.mapping_matches?1:0;v.bad+=r.mapping_exists&&!r.mapping_matches?1:0;
 v.types[r.group_type]+=r.visible_messages;v.visible+=r.visible_messages;v.deleted+=r.deleted_messages;v.total+=r.total_messages;
}
const txt=v=>typeof v==='string'&&v.startsWith('=')?"'"+v:v;
const main=[...roots.values()].map((r,i)=>[i+1,txt(r.code),txt(r.colour??''),r.bad?'Sai mapping':r.mapped===r.groups?'Đủ mapping':r.mapped?'Một phần':'Chưa chuyển',r.groups,r.mapped,...r.types,r.visible,r.deleted,r.total]);
const detail=rows.map(r=>[txt(r.request_code),txt(r.colour_code??''),txt(r.stored_name??r.group_name),r.group_active&&r.root_active?'Có':'Không',r.mapping_exists?r.mapping_matches?'Khớp':'Sai mapping':'Chưa chuyển',r.visible_messages,r.deleted_messages,r.total_messages,r.conversation_id,r.root_id,r.company_id]);
const stamp=new Intl.DateTimeFormat('vi-VN',{timeZone:'Asia/Bangkok',dateStyle:'short',timeStyle:'short'}).format(new Date(source.auditedAtUtc));
const wb=Workbook.create();
const summary=wb.worksheets.add('Theo mã màu');
const groups=wb.worksheets.add('Chi tiết nhóm');
function base(sheet,headers,matrix,widths,name){
 const last=7+matrix.length;
 sheet.showGridLines=false;
 sheet.getRange(`A1:${String.fromCharCode(64+headers.length)}${last}`).format.font={name:'Arial',size:11,color:'#26364A'};
 sheet.getRange(`A1:${String.fromCharCode(64+headers.length)}${last}`).format.rowHeight=23;
 sheet.getRange(`A1:${String.fromCharCode(64+headers.length)}${last}`).format.verticalAlignment='center';
 widths.forEach((w,i)=>sheet.getRange(`${String.fromCharCode(65+i)}:${String.fromCharCode(65+i)}`).format.columnWidth=w);
 sheet.getRange('A2').format.font={name:'Arial',size:16,bold:true,color:'#163E73'};
 sheet.getRange(`A2:${String.fromCharCode(64+headers.length)}2`).format.borders={bottom:{style:'thin',color:'#CAD8E8'}};
 sheet.getRange('A2').format.rowHeight=34;
 sheet.getRange('A3').values=[[`Nguồn: DB theo cấu hình BE ${source.environment}, SELECT chỉ đọc, snapshot ${stamp} (UTC+7).`]];
 sheet.getRange('A3').format.font={name:'Arial',size:10,italic:true,color:'#596B80'};
 sheet.getRange('A4').values=[[source.mappingTableExists?'Trạng thái là mapping đang tồn tại ở thời điểm chụp; DB không lưu ngày chuyển đổi.':'Chưa có bảng nhóm mới trong DB được kiểm tra. Đây là số liệu gốc trước chuyển đổi.']];
 sheet.getRange('A4').format.font={name:'Arial',size:10,color:source.mappingTableExists?'#596B80':'#9A5700'};
 sheet.getRange('A6').values=[['Bao gồm nhóm inactive. Tin còn hiển thị = chưa xóa; tổng tin lưu gồm cả tin đã xóa. Ô mã màu trống = chưa có mã màu.']];
 sheet.getRange('A6').format.font={name:'Arial',size:10,italic:true,color:'#596B80'};
 sheet.getRange(`A7:${String.fromCharCode(64+headers.length)}7`).values=[headers];
 sheet.getRange(`A8:${String.fromCharCode(64+headers.length)}${last}`).values=matrix;
 const table=sheet.tables.add(`A7:${String.fromCharCode(64+headers.length)}${last}`,true,name);
 table.style='TableStyleMedium2';table.showFilterButton=true;
 sheet.getRange(`A7:${String.fromCharCode(64+headers.length)}7`).format={fill:'#244E7E',font:{name:'Arial',size:11,bold:true,color:'#FFFFFF'},horizontalAlignment:'center',verticalAlignment:'center',rowHeight:31};
 sheet.freezePanes.freezeRows(7);
 return last;
}
const end=base(summary,['STT','Mã yêu cầu','Mã màu','Trạng thái','Nhóm hiện có','Mapping khớp','Tin Chung','Tin Kỹ thuật','Tin Giá','Tin nhóm riêng','Tin hiển thị','Tin đã xóa','Tổng tin lưu'],main,[6,18,23,19,16,16,14,16,14,18,17,15,17],'MigrationByColour');
summary.tabColor='#244E7E';summary.getRange('A2').values=[['ĐỐI CHIẾU NHÓM THEO MÃ MÀU']];
summary.getRange('A5').values=[['Hồ sơ']];summary.getRange('B5').formulas=[[`=COUNTA(B8:B${end})`]];
summary.getRange('D5').values=[['Đủ mapping']];summary.getRange('E5').formulas=[[`=COUNTIF(D8:D${end},"Đủ mapping")`]];
summary.getRange('G5').values=[['Tin hiển thị']];summary.getRange('H5').formulas=[[`=SUM(K8:K${end})`]];
summary.getRange('J5').values=[['Tin đã xóa']];summary.getRange('K5').formulas=[[`=SUM(L8:L${end})`]];
summary.getRange('A5:M5').format.font.bold=true;
summary.getRange(`B8:C${end}`).setNumberFormat('@');
summary.getRange(`E8:M${end}`).setNumberFormat('#,##0');
summary.getRange(`E8:M${end}`).format.horizontalAlignment='right';
summary.getRange('G7').format.fill='#2563A0';summary.getRange('H7').format.fill='#0E7490';summary.getRange('I7').format.fill='#166534';
const lastGroup=base(groups,['Mã yêu cầu','Mã màu','Nhóm','Đang dùng','Mapping','Tin hiển thị','Tin đã xóa','Tổng tin lưu','ID hội thoại','ID hồ sơ gốc','ID công ty'],detail,[18,23,27,14,18,17,15,17,39,39,39],'MigrationGroupDetail');
groups.getRange('A2').values=[['SỐ TIN NHẮN TRONG TỪNG NHÓM']];
groups.getRange(`A8:C${lastGroup}`).setNumberFormat('@');groups.getRange(`I8:K${lastGroup}`).setNumberFormat('@');
groups.getRange(`F8:H${lastGroup}`).setNumberFormat('#,##0');groups.getRange(`F8:H${lastGroup}`).format.horizontalAlignment='right';
for(const [sheet,col,last] of [[summary,'D',end],[groups,'E',lastGroup]]){
 sheet.getRange(`${col}8:${col}${last}`).conditionalFormats.add('containsText',{text:'Sai mapping',format:{fill:'#FEE2E2',font:{color:'#991B1B',bold:true}}});
 sheet.getRange(`${col}8:${col}${last}`).conditionalFormats.add('containsText',{text:'Chưa chuyển',format:{fill:'#FFF4DC',font:{color:'#8A5B16'}}});
}
wb.recalculate();
const expectedVisible=rows.reduce((s,r)=>s+r.visible_messages,0),expectedDeleted=rows.reduce((s,r)=>s+r.deleted_messages,0);
if(summary.getRange('B5').values[0][0]!==roots.size || summary.getRange('H5').values[0][0]!==expectedVisible || summary.getRange('K5').values[0][0]!==expectedDeleted)throw new Error('Summary totals differ from source');
if(JSON.stringify(groups.getRange(`A8:K${lastGroup}`).values)!==JSON.stringify(detail))throw new Error('Group detail changed during export');
console.log((await wb.inspect({kind:'table',range:"'Theo mã màu'!A5:M9",include:'values,formulas',tableMaxRows:5,tableMaxCols:13,maxChars:1600})).ndjson);
console.log((await wb.inspect({kind:'match',searchTerm:'#REF!|#DIV/0!|#VALUE!|#NAME\\?|#N/A|#NUM!|#NULL!|#SPILL!|#CALC!',options:{useRegex:true,maxResults:20},summary:'Formula errors'})).ndjson);
for(const [sheet,range,file] of [[summary,'A1:M14','preview-summary.png'],[groups,'A1:H14','preview-groups.png']]){
 const preview=await wb.render({sheetName:sheet.name,range,scale:1.2,format:'png'});
 await fs.writeFile(path.join(directory,file),new Uint8Array(await preview.arrayBuffer()));
}
const dest=path.join(directory,'doi-chieu-nhom-va-tin-nhan.xlsx');
await (await SpreadsheetFile.exportXlsx(wb)).save(dest);
console.log(JSON.stringify({output:dest,roots:roots.size,groups:rows.length,visibleMessages:expectedVisible,deletedMessages:expectedDeleted,mappedGroups:rows.filter(r=>r.mapping_matches).length}));
