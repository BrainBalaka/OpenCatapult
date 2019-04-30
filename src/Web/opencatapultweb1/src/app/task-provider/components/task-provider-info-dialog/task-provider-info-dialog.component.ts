import { Component, OnInit, Inject } from '@angular/core';
import { AdditionalConfigDto, TaskProviderService, TaskProviderDto } from '@app/core';
import { MAT_DIALOG_DATA } from '@angular/material';
import { FormGroup, FormBuilder } from '@angular/forms';

@Component({
  selector: 'app-task-provider-info-dialog',
  templateUrl: './task-provider-info-dialog.component.html',
  styleUrls: ['./task-provider-info-dialog.component.css']
})
export class TaskProviderInfoDialogComponent implements OnInit {
  loading: boolean;
  additionalConfigs: AdditionalConfigDto[];
  providerForm: FormGroup;

  displayedColumns: string[] = ['name', 'label', 'type', 'isRequired', 'isSecret', 'isInputMasked', 'allowedValues', 'hint'];

  constructor (
    private taskProviderService: TaskProviderService,
    private fb: FormBuilder,
    @Inject(MAT_DIALOG_DATA) public taskProvider: TaskProviderDto
    ) {
    }

  ngOnInit() {
    this.providerForm = this.fb.group({
      name: this.taskProvider.name,
      displayName: this.taskProvider.displayName,
      description: this.taskProvider.description,
      author: this.taskProvider.author,
      version: this.taskProvider.version
    });

    this.getTaskProviders();
  }

  getTaskProviders() {
    this.loading = true;
    this.taskProviderService.getTaskProvider(this.taskProvider.id)
      .subscribe(data => {
        this.additionalConfigs = data.additionalConfigs;
        this.loading = false;
      });
  }

}
