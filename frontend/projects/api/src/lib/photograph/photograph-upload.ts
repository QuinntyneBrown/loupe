import { CritiqueBrief } from './critique-brief';

export interface PhotographUpload {
  image: File;
  title: string;
  brief: CritiqueBrief;
  operationKey: string;
}
