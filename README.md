# NasTool-Douban

配合nastool_win_v2.9.x.exe使用,
运行可自动添加豆瓣wish订阅至nastool.

## NasTool-Douban_Setting.XML配置说明

 - <DB_Path></DB_Path>
 
'留空:默认放置于nastool_win_v2.9.x.exe同一目录下,会搜寻/config/user.db

'配置:需要带user.db的完整路径.

- <Douban_Id></Douban_Id>

需要搜寻的Douban_ID.

- <TMDB_API></TMDB_API>

申请一个TMDB_API,用于允许TMDB接口.

- <Douban_SynDays>70</Douban_SynDays>

-1:无日期限制

0～:只搜寻*天前新添加的豆瓣wish.

- <RandomSleep></RandomSleep>

'True/False:启用/关闭随机睡眠（'留空：默认为True，建议开启.）
