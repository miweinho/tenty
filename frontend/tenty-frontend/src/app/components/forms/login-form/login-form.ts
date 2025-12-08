import {ChangeDetectionStrategy, Component, inject, signal} from '@angular/core';
import {takeUntilDestroyed} from '@angular/core/rxjs-interop';
import {FormControl, FormsModule, ReactiveFormsModule, Validators} from '@angular/forms';
import {MatFormFieldModule} from '@angular/material/form-field';
import {MatInputModule} from '@angular/material/input';
import { MatButtonModule } from '@angular/material/button';
import { MatCardModule } from '@angular/material/card';
import {merge} from 'rxjs';
import { Auth } from '../../auth/auth';
import { LoginDTO } from '../../../models';


@Component({
  selector: 'app-login-form',
  imports: [MatCardModule, MatButtonModule, MatFormFieldModule, MatInputModule, FormsModule, ReactiveFormsModule],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './login-form.html',
  styleUrl: './login-form.css',
})
export class LoginForm {
  auth = inject(Auth);
  loading = signal(false);
  readonly email = new FormControl('', [Validators.required, Validators.email]);
  errorMessage = signal('');

  constructor() {
    merge(this.email.statusChanges, this.email.valueChanges)
      .pipe(takeUntilDestroyed())
      .subscribe(() => this.updateErrorMessage());
  }

  updateErrorMessage() {
    if (this.email.hasError('required')) {
      this.errorMessage.set('You must enter a value');
    } else if (this.email.hasError('email')) {
      this.errorMessage.set('Not a valid email');
    } else {
      this.errorMessage.set('');
    }
  }

  
  onSubmit() {
  if(this.email.invalid) return;
  const dto : LoginDTO = {
    mailAddress: this.email.value
  }
  if (this.loading()) return;
  this.loading.set(true);
  this.auth.login(dto).subscribe({
    next: () => { this.loading.set(false); },
    error: () => { this.loading.set(false); this.errorMessage.set('Login fehlgeschlagen'); }
  });
  }
}
