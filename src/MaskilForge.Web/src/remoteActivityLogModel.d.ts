export type ActivityLogDeviceKind = 'phone' | 'tablet' | 'desktop'

export interface RemoteActivityLogSessionLabelInput {
  deviceKind: ActivityLogDeviceKind
  viewportWidth: number
  viewportHeight: number
  standalone: boolean
}

export interface RemoteActivityLogSessionOptionInput extends RemoteActivityLogSessionLabelInput {
  sessionId: string
  lastSeenUtc: string
}

export function activityLogDeviceKind(width: number, height: number, coarsePointer: boolean): ActivityLogDeviceKind
export function remoteActivityLogSessionLabel(session: RemoteActivityLogSessionLabelInput): string
export function remoteActivityLogSessionOptions(
  sessions: RemoteActivityLogSessionOptionInput[],
  formatTime?: (value: string) => string,
): Array<{ sessionId: string; label: string }>
