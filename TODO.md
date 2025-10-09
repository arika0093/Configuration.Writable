* SectionRootNameをやめてSectionNameの直接指定にする(defaultの提供は継続)
* 保存時のValidation機能
* Migration機能
  * ISupportMigration を実装してれば、その値を見てMigrationを行う
* rename AddUserConfigurationFile → AddWritableConfig
* UseStandardSaveLocationの形で UseExecutableDirectory, UseCurrentDirectoryを作る
